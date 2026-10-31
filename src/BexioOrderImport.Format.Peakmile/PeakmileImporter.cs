using System.IO;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Format.Peakmile;

public class PeakmileImporter
{
    private readonly IExcelParser? _excelParser;
    private readonly IBexioService _bexioClient;
    private readonly IPeakmileArticleMatcher _articleMatcher;

    public PeakmileImporter(IBexioService bexioClient, IPeakmileArticleMatcher? articleMatcher = null)
        : this(excelParser: null, bexioClient: bexioClient, articleMatcher: articleMatcher)
    {
    }

    public PeakmileImporter(IExcelParser? excelParser, IBexioService bexioClient, IPeakmileArticleMatcher? articleMatcher = null)
    {
        _excelParser = excelParser;
        _bexioClient = bexioClient ?? throw new ArgumentNullException(nameof(bexioClient));
        _articleMatcher = articleMatcher ?? new PeakmileArticleMatcher(bexioClient);
    }

    public async Task<ImportResult> ExecuteAsync(
        string filePath,
        IImportUserInteractionService interaction,
        PeakmileImportOptions? options = null)
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
        PeakmileImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(interaction);

        options ??= new PeakmileImportOptions();

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

        // All articles are found before Bexio is changed, so a missing article neither creates a customer nor an order
        var articles = await FindArticlesAsync(order, options, interaction);

        int orderId;
        string orderNumberDisplay;
        string titleTemplate = options.DefaultOrderName ?? "Order: {CustomerName} {SeasonCode}";

        var target = await OrderTargetResolver.ResolveAsync(
            order, titleTemplate, options.SeasonCode, _bexioClient, interaction,
            checkContactWithoutCustomerNumber: contactInfo => CheckEmailOfExistingOrderAsync(order, contactInfo, interaction));

        if (target != null)
        {
            if (target.Failure != null)
            {
                return target.Failure;
            }
            orderId = target.OrderId;
            orderNumberDisplay = target.DocumentNr;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(order.Customer.Email))
            {
                interaction.LogInfo("⛔ Email address is required when creating an order by customer email, but no email was provided in the order sheet.");
                throw new InvalidOperationException("Email address is required when no Order Number or Customer Number is specified.");
            }

            int? contactId = (await _bexioClient.FindContactByEmailAsync(order.Customer.Email))?.Id;
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

            var createdOrder = await OrderTargetResolver.CreateOrderAsync(order, contactId.Value, titleTemplate, options.SeasonCode, _bexioClient);
            orderId = createdOrder.Id;
            orderNumberDisplay = createdOrder.DocumentNr;
            interaction.LogInfo($"Order created successfully (Order #{orderNumberDisplay}). Uploading positions...");
            interaction.OrderResolved(orderId, orderNumberDisplay, isNew: true);
        }
        interaction.LogInfo($"Uploading {order.Positions.Count} positions to Bexio...");

        int count = 0;
        foreach (var pos in order.Positions)
        {
            var article = articles[pos];
            string positionText = (options.PositionTextTemplate ?? string.Empty)
                .Replace("{Color}", pos.Color ?? string.Empty)
                .Replace("{SizesRows}", pos.Size ?? string.Empty)
                .Replace("{Size}", pos.Size ?? string.Empty)
                .Replace("{ArticleNumber}", pos.ArticleNumber ?? string.Empty)
                .Replace("{ArticleName}", pos.ArticleName ?? string.Empty)
                .Replace("{BexioArticleName}", article.Name ?? string.Empty)
                .Replace("{BexioArticleDescription}", article.Description ?? string.Empty);

            await _bexioClient.AddArticlePositionAsync(orderId, article.Id, pos, positionText);

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

        interaction.LogInfo($"Successfully completed! Order #{orderNumberDisplay} has been imported into Bexio.");
        return new ImportResult(
            Success: true,
            OrderNumber: orderNumberDisplay,
            UploadedPositionsCount: count);
    }

    /// <summary>
    /// The Bexio article of each position. Throws with all missing articles before anything is changed in Bexio.
    /// </summary>
    private async Task<Dictionary<OrderPosition, BexioArticle>> FindArticlesAsync(Order order, PeakmileImportOptions options, IImportUserInteractionService interaction)
    {
        interaction.LogInfo("Pre-fetching article data from Bexio...");
        string seasonCode = options.SeasonCode ?? string.Empty;
        string[] fallbackSeasonCodes = [.. (options.SeasonFallbackCodes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())];
        string[] allSeasonCodes = [seasonCode, .. fallbackSeasonCodes];
        var articleNumbers = order.Positions.Select(p => p.ArticleNumber).Distinct().ToList();
        foreach (var code in allSeasonCodes)
        {
            await _articleMatcher.PreFetchArticlesAsync(code, articleNumbers);
        }

        var articles = new Dictionary<OrderPosition, BexioArticle>();
        var missing = new List<string>();
        foreach (var pos in order.Positions)
        {
            var match = await _articleMatcher.FindArticleAsync(new PeakmileArticleCriteria(pos.ArticleNumber, pos.Color, seasonCode, fallbackSeasonCodes));
            if (match.IsFound)
            {
                articles[pos] = match.Article;
                continue;
            }

            // The sizes of an article are separate positions; list each article and color once
            string article = $"'{pos.ArticleNumber}' with color '{pos.Color}'";
            if (!missing.Contains(article))
            {
                interaction.LogInfo($"⛔ Article {article} {match.FailureReason} in Bexio.");
                missing.Add(article);
            }
        }

        if (missing.Count > 0)
        {
            string seasonCodes = string.Join("', '", allSeasonCodes);
            throw new InvalidOperationException(
                $"{missing.Count} article(s) not found in Bexio with season code '{seasonCodes}': {string.Join(", ", missing)}. Nothing was changed in Bexio.");
        }
        return articles;
    }

    /// <summary>
    /// Without a customer number, the email of the order form must match the contact of the existing order, or the user accepts the mismatch.
    /// </summary>
    private static async Task<ImportResult?> CheckEmailOfExistingOrderAsync(Order order, BexioContact? contactInfo, IImportUserInteractionService interaction)
    {
        string? existingEmail = contactInfo?.EMail;
        if (string.Equals(existingEmail, order.Customer.Email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        bool ignoreMismatch = await interaction.ConfirmEmailMismatchAsync(existingEmail ?? string.Empty, order.Customer.Email);
        if (!ignoreMismatch)
        {
            interaction.LogInfo($"⛔ Email mismatch: Existing order #{order.OrderNumber} belongs to contact #{contactInfo?.Nr ?? contactInfo?.Id.ToString()} with email {contactInfo?.EMail}, but Email {order.Customer.Email} was specified.");
            return new ImportResult(Success: false, ErrorMessage: $"Email mismatch: Existing order {order.OrderNumber} belongs to contact {contactInfo?.Nr ?? contactInfo?.Id.ToString()} with email {contactInfo?.EMail}, but Email {order.Customer.Email} was specified.");
        }
        interaction.LogInfo("Email mismatch ignored by user. Proceeding with existing order...");
        return null;
    }
}