using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Sgd.Resources;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// SGD (Sports Group Denmark) lists: article number, color, size and quantity per row. The customer and the order
/// are asked for on import; articles and prices come from Bexio.
/// </summary>
public sealed class SgdFormatAdapter : FormatAdapter<SgdSettings>
{
    public const string Id = "sgd";

    public override string FormatId => Id;

    public override string DisplayName => SgdTranslations.Format_DisplayName;

    public override string Description => SgdTranslations.Format_Description;

    protected override Order Parse(string filePath, SgdSettings settings)
        => new SgdExcelParser(settings).ParseOrderForm(filePath);

    protected override Task<ImportResult> ImportAsync(Order order, SgdSettings settings, IBexioService bexio, IImportUserInteractionService interaction)
        => new SgdImporter(bexio).ExecuteAsync(order, settings, interaction);
}
