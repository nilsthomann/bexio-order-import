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
        IImportUserInteractionService interaction,
        ImportOrderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (_excelParser == null)
        {
            throw new InvalidOperationException("IExcelParser is required to parse order from file path.");
        }

        interaction.LogInfo($"Reading Excel file: {Path.GetFileName(filePath)}...");
        var order = _excelParser.ParseOrderForm(filePath);
        interaction.ShowPreview(order);

        return await ExecuteAsync(order, interaction, options);
    }

    public async Task<ImportResult> ExecuteAsync(
        Order order,
        IImportUserInteractionService interaction,
        ImportOrderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(interaction);

        options ??= new ImportOrderOptions();

        if (order.Positions.Count == 0)
        {
            interaction.LogInfo("No order positions with quantity > 0 found.");
            return new ImportResult(Success: false, ErrorMessage: "No positions found.");
        }

        // 1. Ask confirmation
        bool confirmed = await interaction.ConfirmUploadAsync();
        if (!confirmed)
        {
            interaction.LogInfo("Order import cancelled.");
            return new ImportResult(Success: false, ErrorMessage: "Cancelled by user.");
        }

        // 2. Start API upload
        interaction.LogInfo("Connecting to Bexio API...");
        int orderId;

        if (order.OrderId.HasValue)
        {
            interaction.LogInfo($"Checking existing order {order.OrderId.Value} in Bexio...");
            string? existingEmail = await _bexioClient.GetOrderContactEmailAsync(order.OrderId.Value);
            if (existingEmail == null)
            {
                interaction.LogInfo($"⛔ Order with ID {order.OrderId.Value} not found in Bexio.");
                return new ImportResult(Success: false, ErrorMessage: $"Order {order.OrderId.Value} not found.");
            }

            if (!string.Equals(existingEmail, order.Customer.Email, StringComparison.OrdinalIgnoreCase))
            {
                bool ignoreMismatch = await interaction.ConfirmEmailMismatchAsync(existingEmail, order.Customer.Email);
                if (!ignoreMismatch)
                {
                    interaction.LogInfo("Order import cancelled due to email mismatch.");
                    return new ImportResult(Success: false, ErrorMessage: "Cancelled due to email mismatch.");
                }
                interaction.LogInfo("Email mismatch ignored by user. Proceeding with existing order...");
            }

            orderId = order.OrderId.Value;
            interaction.LogInfo($"Existing order matched (Bexio ID: {orderId}). Uploading positions...");
        }
        else
        {
            int? contactId = await _bexioClient.FindContactIdAsync(order.Customer.Email);
            if (!contactId.HasValue)
            {
                bool createCustomerConfirmed = await interaction.ConfirmCustomerCreationAsync(order.Customer);
                if (!createCustomerConfirmed)
                {
                    interaction.LogInfo("Order import cancelled (customer was not created).");
                    return new ImportResult(Success: false, ErrorMessage: "Cancelled (customer creation refused).");
                }
                interaction.LogInfo("Creating new customer in Bexio...");
                contactId = await _bexioClient.CreateContactAsync(order.Customer);
            }
            interaction.LogInfo($"Customer matched (Bexio ID: {contactId.Value}). Creating order...");

            string titleTemplate = options.DefaultOrderName ?? "Order: {CustomerName} {SeasonCode}";
            order.Title = titleTemplate
                .Replace("{CustomerName}", order.Customer.CompanyName ?? string.Empty)
                .Replace("{SeasonCode}", options.SeasonCode ?? string.Empty);

            orderId = await _bexioClient.CreateOrderAsync(contactId.Value, order);
            interaction.LogInfo($"Order created successfully (Bexio ID: {orderId}). Uploading positions...");
        }

        interaction.LogInfo("Pre-fetching article data from Bexio...");
        var uniqueArticleNumbers = order.Positions.Select(p => p.ArticleNumber).Distinct();
        await _bexioClient.PreFetchArticlesAsync(options.SeasonCode ?? string.Empty, uniqueArticleNumbers);

        interaction.LogInfo($"Uploading {order.Positions.Count} positions to Bexio...");

        int count = 0;
        for (int i = 0; i < order.Positions.Count; i++)
        {
            OrderPosition pos = order.Positions[i];

            var article = await _bexioClient.FindArticleAsync(pos.ArticleNumber, pos.Color, options.SeasonCode ?? string.Empty);
            if (article != null)
            {
                string positionText = (options.PositionTextTemplate ?? string.Empty)
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
                interaction.LogInfo($"⛔ Article '{pos.ArticleNumber}' with color '{pos.Color}' and season code '{options.SeasonCode}' not found in Bexio.");
                throw new InvalidOperationException($"Article '{pos.ArticleNumber}' with color '{pos.Color}' and season code '{options.SeasonCode}' not found in Bexio.");
            }

            count++;
            interaction.ReportProgress(count, order.Positions.Count);
            if (count % 5 == 0 || count == order.Positions.Count)
            {
                interaction.LogInfo($"Positions uploaded: {count}/{order.Positions.Count}");
            }
        }

        if (order.DiscountPercent > 0)
        {
            interaction.LogInfo($"Adding global discount position ({order.DiscountPercent:G}%)...");
            string textTemplate = string.IsNullOrWhiteSpace(options.DiscountPositionTextTemplate)
                ? "Rabatt ({DiscountInPercent}%)"
                : options.DiscountPositionTextTemplate;
            string discountText = textTemplate.Replace("{DiscountInPercent}", order.DiscountPercent.ToString("G"));
            await _bexioClient.AddDiscountPositionAsync(orderId, order.DiscountPercent, discountText);
            interaction.LogInfo("Global discount position added successfully.");
        }

        interaction.LogInfo($"Successfully completed! Order #{orderId} has been imported into Bexio.");
        return new ImportResult(
            Success: true,
            OrderId: orderId,
            UploadedPositionsCount: count);
    }
}
