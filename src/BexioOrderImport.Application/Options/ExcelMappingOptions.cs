namespace BexioOrderImport.Application.Options;

public class ExcelMappingOptions
{
    public int WorksheetIndex { get; set; } = 1;
    public string DefaultOrderName { get; set; } = "Order: {CustomerName} {SeasonCode}";
    public string SeasonCode { get; set; } = string.Empty;
    public string PositionTextTemplate { get; set; } = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}";
    public HeaderMapping Header { get; set; } = new();
    public SizeMatrixMapping SizeMatrix { get; set; } = new();
    public DataMapping Data { get; set; } = new();
}

public class HeaderMapping
{
    public string CompanyNameCell { get; set; } = "B4";
    public string StreetCell { get; set; } = "B5";
    public string ZipCityCell { get; set; } = "B6";
    public string BuyerEmailCell { get; set; } = "E5";
    public string BuyerNameCell { get; set; } = "E4";
    public string OrderIdCell { get; set; } = "E6";
    public string PaymentTermsCell { get; set; } = "A9";
    public string DiscountCell { get; set; } = "V12";
}

public class SizeMatrixMapping
{
    public int StartRow { get; set; } = 10;
    public int EndRow { get; set; } = 17;
    public int CategoryColumn { get; set; } = 4;
    public int StartSizeColumn { get; set; } = 5;
    public int EndSizeColumn { get; set; } = 18;
}

public class DataMapping
{
    public int StartRow { get; set; } = 18;
    public int ArticleNumberColumn { get; set; } = 1;
    public int ArticleNameColumn { get; set; } = 2;
    public int ColorColumn { get; set; } = 3;
    public int CategoryColumn { get; set; } = 4;
    public int StartQtyColumn { get; set; } = 5;
    public int EndQtyColumn { get; set; } = 18;
    public int UnitPriceColumn { get; set; } = 20;
}
