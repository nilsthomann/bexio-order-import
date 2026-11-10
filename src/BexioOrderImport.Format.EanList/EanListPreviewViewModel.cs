using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.EanList.Resources;

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

        ShowExternalOrderNumber = settings.UseExternalOrderNumberColumn;
        ExternalOrderNumber = order.ExternalOrderNumber ?? "-";
        HasSeveralExternalOrderNumbers = order.ExternalOrderNumbers.Count > 1;
        SeveralExternalOrderNumbersWarning = HasSeveralExternalOrderNumbers
            ? string.Format(EanListTranslations.Preview_MultipleExternalOrderNumbers, string.Join(", ", order.ExternalOrderNumbers))
            : string.Empty;
        Positions = order.Positions;
        TotalQuantity = order.Positions.Sum(p => p.Quantity * p.SalesUnit);
        SkippedRows = order.SkippedRows;
    }

    public string CustomerNumber { get; }

    /// <summary>
    /// The import is cancelled without a customer number, so the preview warns about it.
    /// </summary>
    public bool IsCustomerNumberMissing { get; }

    public IReadOnlyList<OrderPosition> Positions { get; }

    /// <summary>
    /// Pieces that are imported: quantity × sales unit (VE).
    /// </summary>
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
