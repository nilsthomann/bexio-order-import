using System.Globalization;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Abstractions.Import;

/// <summary>
/// The Bexio order that receives the positions, or why there is none.
/// </summary>
public sealed record OrderTarget(int OrderId, string DocumentNr, ImportResult? Failure = null)
{
    public static OrderTarget Failed(ImportResult failure) => new(0, string.Empty, failure);
}

/// <summary>
/// Finds or creates the Bexio order by the order number and customer number of an order, shared by all formats.
/// </summary>
public static class OrderTargetResolver
{
    /// <summary>
    /// Order number set: uses that existing order; a customer number must belong to its contact.
    /// Otherwise customer number set: creates a new order for that contact.
    /// </summary>
    /// <param name="titleTemplate">Title of a new order with the placeholders {CustomerName} and {SeasonCode}.</param>
    /// <param name="checkContactWithoutCustomerNumber">
    /// Checks the contact of an existing order when no customer number is set; returns a failure to stop the import.
    /// </param>
    /// <param name="useContactNameAsCustomerName">Fills a missing customer name from the Bexio contact (for {CustomerName}).</param>
    /// <returns>Null if neither number is set, so the format decides how to find the customer.</returns>
    public static async Task<OrderTarget?> ResolveAsync(
        Order order,
        string titleTemplate,
        string seasonCode,
        IBexioService bexio,
        IImportUserInteractionService interaction,
        Func<BexioContact?, Task<ImportResult?>>? checkContactWithoutCustomerNumber = null,
        bool useContactNameAsCustomerName = false)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(bexio);
        ArgumentNullException.ThrowIfNull(interaction);

        if (!string.IsNullOrWhiteSpace(order.OrderNumber))
        {
            interaction.LogInfo($"Searching order #{order.OrderNumber} in Bexio...");
            var existingOrder = await bexio.FindOrderByDocumentNrAsync(order.OrderNumber);
            if (existingOrder == null)
            {
                interaction.LogInfo($"⛔ Order #{order.OrderNumber} not found in Bexio.");
                return OrderTarget.Failed(new ImportResult(Success: false, ErrorMessage: $"Order {order.OrderNumber} not found."));
            }

            var contactInfo = await bexio.GetContactDetailsAsync(existingOrder.ContactId);

            if (!string.IsNullOrWhiteSpace(order.CustomerNumber))
            {
                string contactNr = contactInfo?.Nr ?? contactInfo?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                if (!IsSameCustomerNumber(contactNr, order.CustomerNumber))
                {
                    interaction.LogInfo($"⛔ Customer number mismatch: Existing order #{order.OrderNumber} belongs to customer #{contactNr}, but customer #{order.CustomerNumber} was specified.");
                    return OrderTarget.Failed(new ImportResult(Success: false, ErrorMessage: $"Customer number mismatch: order belongs to contact {contactNr}, specified customer number is {order.CustomerNumber}."));
                }
                interaction.LogInfo($"Customer number matched ({order.CustomerNumber}).");
            }
            else if (checkContactWithoutCustomerNumber != null)
            {
                var failure = await checkContactWithoutCustomerNumber(contactInfo);
                if (failure != null)
                {
                    return OrderTarget.Failed(failure);
                }
            }

            interaction.LogInfo($"Existing order matched (Order #{existingOrder.DocumentNr}). Uploading positions...");
            interaction.OrderResolved(existingOrder.Id, existingOrder.DocumentNr, isNew: false);
            return new OrderTarget(existingOrder.Id, existingOrder.DocumentNr);
        }

        if (!string.IsNullOrWhiteSpace(order.CustomerNumber))
        {
            interaction.LogInfo($"Customer number provided ({order.CustomerNumber}). Searching customer in Bexio...");
            var contactInfo = await bexio.FindContactByNrAsync(order.CustomerNumber);
            if (contactInfo == null)
            {
                interaction.LogInfo($"⛔ Customer #{order.CustomerNumber} not found in Bexio.");
                return OrderTarget.Failed(new ImportResult(Success: false, ErrorMessage: $"Customer {order.CustomerNumber} not found."));
            }

            interaction.LogInfo($"Customer found: {contactInfo.Name} ({contactInfo.EMail})");

            if (useContactNameAsCustomerName && string.IsNullOrWhiteSpace(order.Customer.CompanyName))
            {
                order.Customer.CompanyName = contactInfo.Name;
            }

            var createdOrder = await CreateOrderAsync(order, contactInfo.Id, titleTemplate, seasonCode, bexio);
            interaction.LogInfo($"Order created successfully (Order #{createdOrder.DocumentNr}). Uploading positions...");
            interaction.OrderResolved(createdOrder.Id, createdOrder.DocumentNr, isNew: true);
            return new OrderTarget(createdOrder.Id, createdOrder.DocumentNr);
        }

        return null;
    }

    /// <summary>
    /// Sets the order title from the template and creates the order for the contact.
    /// </summary>
    public static Task<BexioOrder> CreateOrderAsync(Order order, int contactId, string titleTemplate, string seasonCode, IBexioService bexio)
    {
        order.Title = (titleTemplate ?? "Order: {CustomerName} {SeasonCode}")
            .Replace("{CustomerName}", order.Customer.CompanyName ?? string.Empty)
            .Replace("{SeasonCode}", seasonCode ?? string.Empty);

        return bexio.CreateOrderAsync(contactId, order);
    }

    /// <summary>
    /// Numeric customer numbers match by value (eg. 01001 and 1001), others by text ignoring case.
    /// </summary>
    private static bool IsSameCustomerNumber(string contactNr, string customerNumber)
    {
        if (int.TryParse(contactNr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int contactValue) &&
            int.TryParse(customerNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out int customerValue))
        {
            return contactValue == customerValue;
        }
        return string.Equals(contactNr.Trim(), customerNumber.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
