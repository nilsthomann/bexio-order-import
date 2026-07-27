using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Models;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Services;

public class ImportOrderUseCase
{
    private readonly IExcelParser? _excelParser;
    private readonly IBexioClient _bexioClient;

    public ImportOrderUseCase(IBexioClient bexioClient)
        : this(excelParser: null, bexioClient: bexioClient)
    {
    }

    public ImportOrderUseCase(IExcelParser? excelParser, IBexioClient bexioClient)
    {
        _excelParser = excelParser;
        _bexioClient = bexioClient ?? throw new ArgumentNullException(nameof(bexioClient));
    }

    public async Task<ImportResult> ExecuteAsync(
        string filePath,
        Action<Order> showPreviewCallback,
        Func<Task<bool>> confirmUploadCallback,
        Func<Customer, Task<bool>> confirmCustomerCreationCallback,
        Func<string, string, Task<bool>> confirmEmailMismatchCallback,
        Action<string> logInfoCallback,
        Action<int, int>? progressCallback = null,
        string defaultOrderName = "Order: {CustomerName} {SeasonCode}",
        string seasonCode = "",
        string positionTextTemplate = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}",
        string discountPositionTextTemplate = "Rabatt ({DiscountInPercent}%)")
    {
        if (_excelParser == null)
        {
            throw new InvalidOperationException("IExcelParser is required to parse order from file path.");
        }

        logInfoCallback($"Reading Excel file: {Path.GetFileName(filePath)}...");
        var order = _excelParser.ParseOrderForm(filePath);

        showPreviewCallback(order);

        return await ExecuteAsync(
            order,
            confirmUploadCallback,
            confirmCustomerCreationCallback,
            confirmEmailMismatchCallback,
            logInfoCallback,
            progressCallback,
            defaultOrderName,
            seasonCode,
            positionTextTemplate,
            discountPositionTextTemplate);
    }

    public async Task<ImportResult> ExecuteAsync(
        Order order,
        Func<Task<bool>> confirmUploadCallback,
        Func<Customer, Task<bool>> confirmCustomerCreationCallback,
        Func<string, string, Task<bool>> confirmEmailMismatchCallback,
        Action<string> logInfoCallback,
        Action<int, int>? progressCallback = null,
        string defaultOrderName = "Order: {CustomerName} {SeasonCode}",
        string seasonCode = "",
        string positionTextTemplate = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}",
        string discountPositionTextTemplate = "Rabatt ({DiscountInPercent}%)")
    {
        if (order.Positions.Count == 0)
        {
            logInfoCallback("No order positions with quantity > 0 found.");
            return new ImportResult(Success: false, ErrorMessage: "No positions found.");
        }

        // 1. Ask confirmation
        bool confirmed = await confirmUploadCallback();
        if (!confirmed)
        {
            logInfoCallback("Order import cancelled.");
            return new ImportResult(Success: false, ErrorMessage: "Cancelled by user.");
        }

        // 2. Start API upload
        logInfoCallback("Connecting to Bexio API...");
        int orderId;

        if (order.OrderId.HasValue)
        {
            logInfoCallback($"Checking existing order {order.OrderId.Value} in Bexio...");
            string? existingEmail = await _bexioClient.GetOrderContactEmailAsync(order.OrderId.Value);
            if (existingEmail == null)
            {
                logInfoCallback($"⛔ Order with ID {order.OrderId.Value} not found in Bexio.");
                return new ImportResult(Success: false, ErrorMessage: $"Order {order.OrderId.Value} not found.");
            }

            if (!string.Equals(existingEmail, order.Customer.Email, StringComparison.OrdinalIgnoreCase))
            {
                bool ignoreMismatch = await confirmEmailMismatchCallback(existingEmail, order.Customer.Email);
                if (!ignoreMismatch)
                {
                    logInfoCallback("Order import cancelled due to email mismatch.");
                    return new ImportResult(Success: false, ErrorMessage: "Cancelled due to email mismatch.");
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
                    return new ImportResult(Success: false, ErrorMessage: "Cancelled (customer creation refused).");
                }
                logInfoCallback("Creating new customer in Bexio...");
                contactId = await _bexioClient.CreateContactAsync(order.Customer);
            }
            logInfoCallback($"Customer matched (Bexio ID: {contactId.Value}). Creating order...");

            string titleTemplate = defaultOrderName ?? "Order: {CustomerName} {SeasonCode}";
            order.Title = titleTemplate
                .Replace("{CustomerName}", order.Customer.CompanyName ?? string.Empty)
                .Replace("{SeasonCode}", seasonCode ?? string.Empty);

            orderId = await _bexioClient.CreateOrderAsync(contactId.Value, order);
            logInfoCallback($"Order created successfully (Bexio ID: {orderId}). Uploading positions...");
        }

        logInfoCallback("Pre-fetching article data from Bexio...");
        var uniqueArticleNumbers = order.Positions.Select(p => p.ArticleNumber).Distinct();
        await _bexioClient.PreFetchArticlesAsync(seasonCode ?? string.Empty, uniqueArticleNumbers);

        logInfoCallback($"Uploading {order.Positions.Count} positions to Bexio...");

        int count = 0;
        for (int i = 0; i < order.Positions.Count; i++)
        {
            OrderPosition pos = order.Positions[i];

            var article = await _bexioClient.FindArticleAsync(pos.ArticleNumber, pos.Color, seasonCode ?? string.Empty);
            if (article != null)
            {
                string positionText = (positionTextTemplate ?? string.Empty)
                    .Replace("{Color}", pos.Color ?? string.Empty)
                    .Replace("{Size}", pos.Size ?? string.Empty)
                    .Replace("{ArticleNumber}", pos.ArticleNumber ?? string.Empty)
                    .Replace("{ArticleName}", pos.ArticleName ?? string.Empty)
                    .Replace("{BexioArticleName}", article.Name ?? string.Empty)
                    .Replace("{BexioArticleDescription}", article.Description ?? string.Empty);

                await _bexioClient.AddArticlePositionAsync(orderId, article.Id, pos, positionText);
            }
            else
            {
                logInfoCallback($"⛔ Article '{pos.ArticleNumber}' with color '{pos.Color}' and season code '{seasonCode}' not found in Bexio.");
                throw new InvalidOperationException($"Article '{pos.ArticleNumber}' with color '{pos.Color}' and season code '{seasonCode}' not found in Bexio.");
            }

            count++;
            progressCallback?.Invoke(count, order.Positions.Count);
            if (count % 5 == 0 || count == order.Positions.Count)
            {
                logInfoCallback($"Positions uploaded: {count}/{order.Positions.Count}");
            }
        }

        if (order.DiscountPercent > 0)
        {
            logInfoCallback($"Adding global discount position ({order.DiscountPercent:G}%)...");
            string textTemplate = string.IsNullOrWhiteSpace(discountPositionTextTemplate)
                ? "Rabatt ({DiscountInPercent}%)"
                : discountPositionTextTemplate;
            string discountText = textTemplate.Replace("{DiscountInPercent}", order.DiscountPercent.ToString("G"));
            await _bexioClient.AddDiscountPositionAsync(orderId, order.DiscountPercent, discountText);
            logInfoCallback("Global discount position added successfully.");
        }

        logInfoCallback($"Successfully completed! Order #{orderId} has been imported into Bexio.");
        return new ImportResult(
            Success: true,
            OrderId: orderId,
            UploadedPositionsCount: count);
    }
}
