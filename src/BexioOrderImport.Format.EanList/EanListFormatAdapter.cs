using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.EanList.Resources;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// EAN lists: EAN, quantity and customer number per row; articles and prices come from Bexio.
/// </summary>
public sealed class EanListFormatAdapter : FormatAdapter<EanListSettings>
{
    public const string Id = "ean-list";

    public override string FormatId => Id;

    public override string DisplayName => EanListTranslations.Format_DisplayName;

    public override string Description => EanListTranslations.Format_Description;

    protected override Order Parse(string filePath, EanListSettings settings)
        => new EanListExcelParser(settings).ParseOrderForm(filePath);

    protected override Task<ImportResult> ImportAsync(Order order, EanListSettings settings, IBexioService bexio, IImportUserInteractionService interaction)
        => new EanListImporter(bexio).ExecuteAsync(order, settings, interaction);
}
