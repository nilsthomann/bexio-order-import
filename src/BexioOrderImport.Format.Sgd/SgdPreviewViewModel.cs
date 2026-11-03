using System.Globalization;
using BexioOrderImport.Domain.Models;

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
}
