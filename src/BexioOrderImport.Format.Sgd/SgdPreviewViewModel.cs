using System.Globalization;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Sgd.Resources;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Season code, discount and the positions of a parsed SGD list. Bexio is not called before the import.
/// </summary>
public sealed class SgdPreviewViewModel
{
    public SgdPreviewViewModel(Order order, SgdSettings settings)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);

        SeasonCode = string.IsNullOrWhiteSpace(settings.SeasonCode) ? "-" : settings.SeasonCode;
        Discount = settings.DiscountPercent > 0 ? $"{settings.DiscountPercent.ToString("0.##", CultureInfo.CurrentCulture)} %" : "-";
        ShowExternalOrderNumber = settings.UseExternalOrderNumberColumn;
        ExternalOrderNumber = order.ExternalOrderNumber ?? "-";
        HasSeveralExternalOrderNumbers = order.ExternalOrderNumbers.Count > 1;
        SeveralExternalOrderNumbersWarning = HasSeveralExternalOrderNumbers
            ? string.Format(SgdTranslations.Preview_MultipleExternalOrderNumbers, string.Join(", ", order.ExternalOrderNumbers))
            : string.Empty;
        Positions = order.Positions;
        TotalQuantity = order.Positions.Sum(p => p.Quantity);
        SkippedRows = order.SkippedRows;
    }

    public string SeasonCode { get; }

    /// <summary>
    /// Discount of the profile, eg. "10 %", or "-" without discount.
    /// </summary>
    public string Discount { get; }

    public IReadOnlyList<OrderPosition> Positions { get; }

    public int TotalQuantity { get; }

    public int SkippedRows { get; }

    /// <summary>
    /// The profile reads the external order number column, so the header shows it.
    /// </summary>
    public bool ShowExternalOrderNumber { get; }

    /// <summary>
    /// The first external order number of the file, or "-" without one.
    /// </summary>
    public string ExternalOrderNumber { get; }

    public bool HasSeveralExternalOrderNumbers { get; }

    public string SeveralExternalOrderNumbersWarning { get; }
}
