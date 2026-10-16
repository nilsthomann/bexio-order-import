using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Format.Peakmile;

/// <summary>
/// Header, positions and totals of a parsed Peakmile order, as shown before the import.
/// </summary>
public sealed class PeakmilePreviewViewModel
{
    public PeakmilePreviewViewModel(Order order, PeakmileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(settings);

        CompanyName = OrDash(order.Customer.CompanyName);
        BuyerName = OrDash(order.Customer.BuyerName);
        Email = OrDash(order.Customer.Email);

        string street = order.Customer.Street?.Trim() ?? string.Empty;
        string zip = order.Customer.ZipCode?.Trim() ?? string.Empty;
        string city = order.Customer.City?.Trim() ?? string.Empty;
        Address = OrDash($"{street}, {zip} {city}".Trim(',', ' '));

        OrderNumber = OrDash(order.OrderNumber);
        CustomerNumber = OrDash(order.CustomerNumber);
        PaymentTerms = OrDash(order.PaymentTerms);

        Positions = order.Positions;
        IsRowDiscountEnabled = settings.Data.EnableRowDiscount;

        TotalQuantity = order.Positions.Sum(p => p.Quantity);
        TotalGrossAmount = order.Positions.Sum(p => p.TotalPrice);
        DiscountPercent = order.DiscountPercent;
        TotalNetAmount = TotalGrossAmount - TotalGrossAmount * (DiscountPercent / 100m);
    }

    public string CompanyName { get; }
    public string BuyerName { get; }
    public string Email { get; }
    public string Address { get; }
    public string OrderNumber { get; }
    public string CustomerNumber { get; }
    public string PaymentTerms { get; }

    public IReadOnlyList<OrderPosition> Positions { get; }

    /// <summary>
    /// Shows gross price, discount and net price per position instead of the unit price.
    /// </summary>
    public bool IsRowDiscountEnabled { get; }

    public int TotalQuantity { get; }
    public decimal TotalGrossAmount { get; }
    public decimal DiscountPercent { get; }
    public decimal TotalNetAmount { get; }

    private static string OrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
