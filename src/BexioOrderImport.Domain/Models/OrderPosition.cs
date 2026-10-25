namespace BexioOrderImport.Domain.Models;

public class OrderPosition
{
    public string ArticleNumber { get; set; } = string.Empty;

    /// <summary>
    /// EAN barcode of the article, set by formats that identify articles by EAN.
    /// </summary>
    public string? Ean { get; set; }

    /// <summary>
    /// Excel row the position was read from, for messages. 0 if unknown.
    /// </summary>
    public int SourceRow { get; set; }
    public string ArticleName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string SizeCategory { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>
    /// Pieces per sales unit (VE) of formats that order in sales units; Bexio gets Quantity × SalesUnit pieces.
    /// </summary>
    public int SalesUnit { get; set; } = 1;
    public decimal GrossUnitPrice { get; set; }
    public decimal? DiscountPercent { get; set; }

    public decimal NetUnitPrice => DiscountPercent.HasValue && DiscountPercent.Value > 0
        ? RoundUp(GrossUnitPrice * (1m - DiscountPercent.Value / 100m), 1)
        : GrossUnitPrice;

    public decimal UnitPrice
    {
        get => NetUnitPrice;
        set => GrossUnitPrice = value;
    }

    public decimal TotalPrice => Quantity * NetUnitPrice;


    /// <summary>
    /// Round up like the Excel ROUNDUP function
    /// </summary>
    /// <param name="number">Number to round up</param>
    /// <param name="digits">Amount of digits</param>
    /// <returns></returns>
    private static decimal RoundUp(decimal number, int digits)
    {
        decimal factor = (decimal)Math.Pow(10, digits);

        // Round away from zero, matching Excel
        return Math.Sign(number) * Math.Ceiling(Math.Abs(number) * factor) / factor;
    }
}
