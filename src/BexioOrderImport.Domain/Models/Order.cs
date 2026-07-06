namespace BexioOrderImport.Domain.Models;

public class Order
{
    public Customer Customer { get; set; } = new();
    public DateTime? DeliveryDate { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public List<OrderPosition> Positions { get; set; } = [];
    public decimal TotalAmount => Positions.Sum(p => p.TotalPrice);
    public decimal TotalNetAmount => TotalAmount * (1 - DiscountPercent / 100m);
    public int TotalQuantity => Positions.Sum(p => p.Quantity);
}
