using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Services;

public class ImportOrderUseCase
{
    private readonly IExcelParser _excelParser;
    private readonly IBexioClient _bexioClient;

    public ImportOrderUseCase(IExcelParser excelParser, IBexioClient bexioClient)
    {
        _excelParser = excelParser;
        _bexioClient = bexioClient;
    }

    public async Task<bool> ExecuteAsync(
        string filePath,
        Action<Order> showPreviewCallback,
        Func<Task<bool>> confirmUploadCallback,
        Func<Customer, Task<bool>> confirmCustomerCreationCallback,
        Func<string, string, Task<bool>> confirmEmailMismatchCallback,
        Action<string> logInfoCallback,
        string defaultOrderName = "Order: {CustomerName} {SeasonCode}",
        string seasonCode = "",
        string positionTextTemplate = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}")
    {
        logInfoCallback($"Reading Excel file: {Path.GetFileName(filePath)}...");
        var order = _excelParser.ParseOrderForm(filePath);

        if (order.Positions.Count == 0)
        {
            logInfoCallback("No order positions with quantity > 0 found.");
            return false;
        }

        // 1. Show preview
        showPreviewCallback(order);

        // 2. Ask confirmation
        bool confirmed = await confirmUploadCallback();
        if (!confirmed)
        {
            logInfoCallback("Order import cancelled.");
            return false;
        }

        // 3. Start API upload
        logInfoCallback("Connecting to Bexio API...");
        int orderId;

        if (order.OrderId.HasValue)
        {
            logInfoCallback($"Checking existing order {order.OrderId.Value} in Bexio...");
            string? existingEmail = await _bexioClient.GetOrderContactEmailAsync(order.OrderId.Value);
            if (existingEmail == null)
            {
                logInfoCallback($"[red]Error:[/] Order with ID {order.OrderId.Value} not found in Bexio.");
                return false;
            }

            if (!string.Equals(existingEmail, order.Customer.Email, StringComparison.OrdinalIgnoreCase))
            {
                bool ignoreMismatch = await confirmEmailMismatchCallback(existingEmail, order.Customer.Email);
                if (!ignoreMismatch)
                {
                    logInfoCallback("Order import cancelled due to email mismatch.");
                    return false;
                }
                logInfoCallback("Email mismatch ignored by user. Proceeding with existing order...");
            }

            orderId = order.OrderId.Value;
            logInfoCallback($"Existing order matched (Bexio ID: {orderId}). Uploading positions...");
        }
        else
        {
            int? contactId = await _bexioClient.FindContactIdAsync(order.Customer.Email);
            if (!contactId.HasValue)
            {
                bool createCustomerConfirmed = await confirmCustomerCreationCallback(order.Customer);
                if (!createCustomerConfirmed)
                {
                    logInfoCallback("Order import cancelled (customer was not created).");
                    return false;
                }
                logInfoCallback("Creating new customer in Bexio...");
                contactId = await _bexioClient.CreateContactAsync(order.Customer);
            }
            logInfoCallback($"Customer matched (Bexio ID: {contactId.Value}). Creating order...");

            string titleTemplate = defaultOrderName ?? "Order: {CustomerName} {SeasonCode}";
            order.Title = titleTemplate
                .Replace("{CustomerName}", order.Customer.CompanyName ?? "")
                .Replace("{SeasonCode}", seasonCode ?? "");

            orderId = await _bexioClient.CreateOrderAsync(contactId.Value, order);
            logInfoCallback($"Order created successfully (Bexio ID: {orderId}). Uploading positions...");
        }

        int count = 0;
        for (int i = 0; i < order.Positions.Count; i++)
        {
            OrderPosition pos = order.Positions[i];
            
            // ponytail: format search query as "{SeasonCode} {ArticleNo} {Color}"
            string searchQuery = $"{seasonCode} {pos.ArticleNumber} {pos.Color}".Trim();
            var article = await _bexioClient.FindArticleAsync(searchQuery);
            if (article != null)
            {
                // ponytail: inline placeholder replacement instead of complex regex compiler
                pos.PositionText = (positionTextTemplate ?? string.Empty)
                    .Replace("{Color}", pos.Color ?? string.Empty)
                    .Replace("{Size}", pos.Size ?? string.Empty)
                    .Replace("{ArticleNumber}", pos.ArticleNumber ?? string.Empty)
                    .Replace("{ArticleName}", pos.ArticleName ?? string.Empty)
                    .Replace("{BexioArticleName}", article.InternName ?? string.Empty)
                    .Replace("{BexioArticleDescription}", article.Text ?? string.Empty);


                await _bexioClient.AddArticlePositionAsync(orderId, article.Id, pos);
            }
            else
            {
                logInfoCallback($"[red]Error:[/] Article '{pos.ArticleNumber}' ({pos.ArticleName}) not found in Bexio.");
                throw new InvalidOperationException($"Article '{pos.ArticleNumber}' ({pos.ArticleName}) not found in Bexio.");
            }

            count++;
            if (count % 5 == 0 || count == order.Positions.Count)
            {
                logInfoCallback($"Positions uploaded: {count}/{order.Positions.Count}");
            }
        }

        logInfoCallback($"Successfully completed! Order #{orderId} has been imported into Bexio.");
        return true;
    }
}
