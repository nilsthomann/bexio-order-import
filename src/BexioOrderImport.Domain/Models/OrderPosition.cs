namespace BexioOrderImport.Domain.Models;

public class OrderPosition
{
    public string ArticleNumber { get; set; } = string.Empty;
    public string ArticleName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string SizeCategory { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public string PositionText { get; set; } = string.Empty;
    public decimal TotalPrice => Quantity * UnitPrice;
}
