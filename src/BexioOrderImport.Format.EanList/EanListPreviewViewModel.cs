using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// Customer number and the positions of a parsed EAN list. Bexio is not called before the import.
/// </summary>
public sealed class EanListPreviewViewModel
{
    public EanListPreviewViewModel(Order order, EanListSettings settings)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);

        IsCustomerNumberMissing = string.IsNullOrWhiteSpace(order.CustomerNumber);
        CustomerNumber = IsCustomerNumberMissing ? "-" : order.CustomerNumber!;

        Positions = order.Positions;
        TotalQuantity = order.Positions.Sum(p => p.Quantity);
        SkippedRows = order.SkippedRows;
    }

    public string CustomerNumber { get; }

    /// <summary>
    /// The import is cancelled without a customer number, so the preview warns about it.
    /// </summary>
    public bool IsCustomerNumberMissing { get; }

    public IReadOnlyList<OrderPosition> Positions { get; }

    public int TotalQuantity { get; }

    public int SkippedRows { get; }
}
