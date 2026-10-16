namespace BexioOrderImport.Domain.Models;

public class Order
{
    public string Title { get; set; } = string.Empty;
    public Customer Customer { get; set; } = new();
    public string? OrderNumber { get; set; }
    public string? CustomerNumber { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public List<OrderPosition> Positions { get; set; } = [];

    /// <summary>
    /// Rows the parser ignored, eg. because their quantity was empty or 0.
    /// </summary>
    public int SkippedRows { get; set; }
    public decimal TotalAmount => Positions.Sum(p => p.TotalPrice);
    public decimal TotalNetAmount => TotalAmount * (1 - DiscountPercent / 100m);
    public int TotalQuantity => Positions.Sum(p => p.Quantity);
}
