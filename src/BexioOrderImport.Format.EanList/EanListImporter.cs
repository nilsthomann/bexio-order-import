using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// Imports an EAN list: the customer is found by customer number only, articles by EAN (Bexio intern_code) and prices come from Bexio.
/// </summary>
public sealed class EanListImporter
{
    private readonly IBexioService _bexio;
    private readonly IArticleMatcher<EanArticleCriteria> _articleMatcher;

    public EanListImporter(IBexioService bexio, IArticleMatcher<EanArticleCriteria>? articleMatcher = null)
    {
        _bexio = bexio ?? throw new ArgumentNullException(nameof(bexio));
        _articleMatcher = articleMatcher ?? new EanArticleMatcher(bexio);
    }

    public async Task<ImportResult> ExecuteAsync(Order order, EanListSettings settings, IImportUserInteractionService interaction)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(interaction);

        // Without a customer number there is no email or customer creation fallback
        if (string.IsNullOrWhiteSpace(order.CustomerNumber))
        {
            interaction.LogInfo("⛔ No customer number in the profile.");
            return new ImportResult(Success: false, ErrorMessage: "Customer number missing.");
        }

        if (order.Positions.Count == 0)
        {
            interaction.LogInfo("No order positions with quantity > 0 found.");
            return new ImportResult(Success: false, ErrorMessage: "No positions found.");
        }

        if (!await interaction.ConfirmUploadAsync())
        {
            interaction.LogInfo("Order import cancelled.");
            return new ImportResult(Success: false, ErrorMessage: "Cancelled by user.");
        }

        interaction.LogInfo("Connecting to Bexio API...");
        var target = await OrderTargetResolver.ResolveAsync(
            order, settings.DefaultOrderName, string.Empty, _bexio, interaction, useContactNameAsCustomerName: true);
        if (target!.Failure != null)
        {
            return target.Failure;
        }

        interaction.LogInfo($"Uploading {order.Positions.Count} positions to Bexio...");
        int count = 0;
        foreach (var position in order.Positions)
        {
            string ean = position.Ean ?? string.Empty;
            var match = await _articleMatcher.FindArticleAsync(new EanArticleCriteria(ean));
            if (!match.IsFound)
            {
                interaction.LogInfo($"⛔ EAN '{ean}' (row {position.SourceRow}) {match.FailureReason} in Bexio.");
                throw new InvalidOperationException($"EAN '{ean}' (row {position.SourceRow}) {match.FailureReason} in Bexio.");
            }

            var article = match.Article;
            if (article.SalePrice == null)
            {
                interaction.LogInfo($"⚠️ Article '{article.Code}' has no sale price in Bexio.");
            }
            position.GrossUnitPrice = article.SalePrice ?? 0m;
            position.ArticleName = article.Name;

            // {Ean} is the older spelling, profiles may still use it
            string positionText = (settings.PositionTextTemplate ?? string.Empty)
                .Replace("{EAN}", ean)
                .Replace("{Ean}", ean)
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

        interaction.LogInfo($"Successfully completed! Order #{target.DocumentNr} has been imported into Bexio.");
        return new ImportResult(Success: true, OrderNumber: target.DocumentNr, UploadedPositionsCount: count);
    }
}
