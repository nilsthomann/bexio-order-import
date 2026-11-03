using System.Globalization;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Format.Sgd.Resources;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Imports an SGD list: asks for the customer number and whether to create a new order or to replace the positions
/// of an existing one. Articles are found like Peakmile articles, prices come from Bexio.
/// </summary>
public sealed class SgdImporter
{
    private const int ExistingOrderOption = 1;

    private readonly IBexioService _bexio;
    private readonly ISgdArticleMatcher _articleMatcher;

    public SgdImporter(IBexioService bexio, ISgdArticleMatcher? articleMatcher = null)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
        _articleMatcher = articleMatcher ?? new SgdArticleMatcher(bexio);
    }

    public async Task<ImportResult> ExecuteAsync(Order order, SgdSettings settings, IImportUserInteractionService interaction)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(interaction);

        if (order.Positions.Count == 0)
        {
            interaction.LogInfo("No order positions with quantity > 0 found.");
            return new ImportResult(Success: false, ErrorMessage: "No positions found.");
        }

        interaction.LogInfo("Connecting to Bexio API...");
        var contact = await AskCustomerAsync(interaction);
        if (contact == null)
        {
            return Cancelled(interaction);
        }

        int? choice = await interaction.ChooseAsync(
            SgdTranslations.Import_OrderChoiceTitle,
            string.Format(SgdTranslations.Import_OrderChoiceMessage, contact.Value.Contact.Name),
            [SgdTranslations.Import_NewOrder, SgdTranslations.Import_ExistingOrder]);
        if (choice == null)
        {
            return Cancelled(interaction);
        }

        bool replaceExistingOrder = choice == ExistingOrderOption;
        order.CustomerNumber = contact.Value.CustomerNumber;
        order.OrderNumber = null;
        if (replaceExistingOrder)
        {
            string? orderNumber = await AskOrderNumberAsync(interaction);
            if (orderNumber == null || !await interaction.ConfirmWarningAsync(
                SgdTranslations.Import_ReplaceWarningTitle, string.Format(SgdTranslations.Import_ReplaceWarningMessage, orderNumber)))
            {
                return Cancelled(interaction);
            }
            order.OrderNumber = orderNumber;
        }

        // All articles are found before Bexio is changed, so a missing article neither creates an order nor deletes positions
        var articles = await FindArticlesAsync(order, settings, interaction);

        var target = await OrderTargetResolver.ResolveAsync(
            order, settings.DefaultOrderName, settings.SeasonCode, _bexio, interaction, useContactNameAsCustomerName: true);
        if (target!.Failure != null)
        {
            return target.Failure;
        }

        if (replaceExistingOrder)
        {
            interaction.LogInfo($"Deleting all positions of order #{target.DocumentNr}...");
            // Reported first: a failed delete can leave the order partly emptied
            interaction.OrderPositionsDeleted();
            int deleted = await _bexio.DeleteAllPositionsAsync(target.OrderId);
            interaction.LogInfo($"{deleted} positions deleted.");
        }

        interaction.LogInfo($"Uploading {order.Positions.Count} positions to Bexio...");
        string template = settings.PositionGroupingMode == SgdPositionGroupingMode.GroupedSizePosition
            ? settings.GroupedPositionTextTemplate
            : settings.SinglePositionTextTemplate;
        int count = 0;
        foreach (var position in order.Positions)
        {
            var article = articles[position];
            if (article.SalePrice == null)
            {
                interaction.LogInfo($"⚠️ Article '{article.Code}' has no sale price in Bexio.");
            }
            position.GrossUnitPrice = article.SalePrice ?? 0m;

            string positionText = (template ?? string.Empty)
                .Replace("{Color}", position.Color)
                .Replace("{SizesRows}", position.Size)
                .Replace("{Size}", position.Size)
                .Replace("{ArticleNumber}", position.ArticleNumber)
                .Replace("{ArticleName}", position.ArticleName)
                .Replace("{BexioArticleName}", article.Name ?? string.Empty)
                .Replace("{BexioArticleDescription}", article.Description ?? string.Empty);
            await _bexio.AddArticlePositionAsync(target.OrderId, article.Id, position, positionText);

            count++;
            interaction.ReportProgress(count, order.Positions.Count);
            if (count % 5 == 0 || count == order.Positions.Count)
            {
                interaction.LogInfo($"Positions uploaded: {count}/{order.Positions.Count}");
            }
        }

        if (settings.DiscountPercent > 0)
        {
            string discount = settings.DiscountPercent.ToString("G", CultureInfo.InvariantCulture);
            interaction.LogInfo($"Adding discount position ({discount}%)...");
            string textTemplate = string.IsNullOrWhiteSpace(settings.DiscountPositionTextTemplate) ? "Rabatt ({DiscountInPercent}%)" : settings.DiscountPositionTextTemplate;
            await _bexio.AddDiscountPositionAsync(target.OrderId, settings.DiscountPercent, textTemplate.Replace("{DiscountInPercent}", discount));
        }

        interaction.LogInfo($"Successfully completed! Order #{target.DocumentNr} has been imported into Bexio.");
        return new ImportResult(Success: true, OrderNumber: target.DocumentNr, UploadedPositionsCount: count);
    }

    private static ImportResult Cancelled(IImportUserInteractionService interaction)
    {
        interaction.LogInfo("Order import cancelled.");
        return new ImportResult(Success: false, ErrorMessage: "Cancelled by user.");
    }

    /// <summary>
    /// Asks until the customer number of a Bexio contact is entered. Null if the user cancels.
    /// </summary>
    private async Task<(string CustomerNumber, BexioContact Contact)?> AskCustomerAsync(IImportUserInteractionService interaction)
    {
        string? errorMessage = null;
        while (true)
        {
            string? customerNumber = (await interaction.AskTextAsync(
                SgdTranslations.Import_CustomerTitle, SgdTranslations.Import_CustomerMessage, errorMessage))?.Trim();
            if (customerNumber == null) return null;

            if (customerNumber.Length == 0)
            {
                errorMessage = SgdTranslations.Import_CustomerRequired;
                continue;
            }

            interaction.LogInfo($"Searching customer #{customerNumber} in Bexio...");
            var contact = await _bexio.FindContactByNrAsync(customerNumber);
            if (contact != null)
            {
                interaction.LogInfo($"Customer found: {contact.Name}");
                return (customerNumber, contact);
            }

            interaction.LogInfo($"⛔ Customer #{customerNumber} not found in Bexio.");
            errorMessage = string.Format(SgdTranslations.Import_CustomerNotFound, customerNumber);
        }
    }

    /// <summary>
    /// Asks for the number of the order to update. Null if the user cancels. The resolver checks that it exists.
    /// </summary>
    private static async Task<string?> AskOrderNumberAsync(IImportUserInteractionService interaction)
    {
        string? errorMessage = null;
        while (true)
        {
            string? orderNumber = (await interaction.AskTextAsync(
                SgdTranslations.Import_OrderNumberTitle, SgdTranslations.Import_OrderNumberMessage, errorMessage))?.Trim();
            if (orderNumber == null || orderNumber.Length > 0) return orderNumber;

            errorMessage = SgdTranslations.Import_OrderNumberRequired;
        }
    }

    /// <summary>
    /// The Bexio article of each position. Throws with all missing articles before anything is changed in Bexio.
    /// </summary>
    private async Task<Dictionary<OrderPosition, BexioArticle>> FindArticlesAsync(Order order, SgdSettings settings, IImportUserInteractionService interaction)
    {
        interaction.LogInfo("Pre-fetching article data from Bexio...");
        string seasonCode = settings.SeasonCode ?? string.Empty;
        string[] fallbackSeasonCodes = [.. (settings.SeasonFallbackCodes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())];
        var articleNumbers = order.Positions.Select(p => p.ArticleNumber).Distinct().ToList();
        foreach (var code in (string[])[seasonCode, .. fallbackSeasonCodes])
        {
            await _articleMatcher.PreFetchArticlesAsync(code, articleNumbers);
        }

        var articles = new Dictionary<OrderPosition, BexioArticle>();
        var missing = new List<string>();
        foreach (var position in order.Positions)
        {
            var match = await _articleMatcher.FindArticleAsync(new SgdArticleCriteria(position.ArticleNumber, position.Color, seasonCode, fallbackSeasonCodes));
            if (match.IsFound)
            {
                articles[position] = match.Article;
                continue;
            }

            string article = $"'{position.ArticleNumber}' with color '{position.Color}' (row {position.SourceRow})";
            interaction.LogInfo($"⛔ Article {article} {match.FailureReason} in Bexio.");
            missing.Add(article);
        }

        if (missing.Count > 0)
        {
            string seasonCodes = string.Join("', '", [seasonCode, .. fallbackSeasonCodes]);
            throw new InvalidOperationException(
                $"{missing.Count} article(s) not found in Bexio with season code '{seasonCodes}': {string.Join(", ", missing)}. Nothing was changed in Bexio.");
        }
        return articles;
    }
}
