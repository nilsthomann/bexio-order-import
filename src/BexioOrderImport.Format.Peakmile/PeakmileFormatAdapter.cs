using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Peakmile.Resources;

namespace BexioOrderImport.Format.Peakmile;

/// <summary>
/// Peakmile order forms: size matrix, position grouping, discounts and customer lookup by email, customer or order number.
/// </summary>
public sealed class PeakmileFormatAdapter : FormatAdapter<PeakmileSettings>
{
    public const string Id = "peakmile";

    private readonly Func<PeakmileSettings, IExcelParser> _createParser;

    public PeakmileFormatAdapter() : this(settings => new PeakmileExcelParser(settings))
    {
    }

    internal PeakmileFormatAdapter(Func<PeakmileSettings, IExcelParser> createParser)
    {
        _createParser = createParser ?? throw new ArgumentNullException(nameof(createParser));
    }

    public override string FormatId => Id;

    public override string DisplayName => PeakmileTranslations.Format_DisplayName;

    public override string Description => PeakmileTranslations.Format_Description;

    protected override Order Parse(string filePath, PeakmileSettings settings)
        => _createParser(settings).ParseOrderForm(filePath);

    protected override Task<ImportResult> ImportAsync(Order order, PeakmileSettings settings, IBexioService bexio, IImportUserInteractionService interaction)
        => new PeakmileImporter(bexio).ExecuteAsync(order, interaction, CreateImportOptions(settings));

    internal static PeakmileImportOptions CreateImportOptions(PeakmileSettings settings) => new(
        DefaultOrderName: settings.DefaultOrderName,
        SeasonCode: settings.SeasonCode,
        PositionTextTemplate: settings.PositionTextTemplate,
        DiscountPositionTextTemplate: settings.DiscountPositionTextTemplate,
        SizeRowTemplate: settings.SizeRowTemplate,
        SeasonFallbackCodes: settings.SeasonFallbackCodes);
}
