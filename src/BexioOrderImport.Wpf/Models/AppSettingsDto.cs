using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BexioOrderImport.Wpf.Models;

public class AppSettingsDto
{
    [JsonPropertyName("Bexio")]
    public BexioSettingsDto Bexio { get; set; } = new();

    [JsonPropertyName("ActiveProfileName")]
    public string ActiveProfileName { get; set; } = "Default";

    [JsonPropertyName("Profiles")]
    public List<MappingProfileDto> Profiles { get; set; } = new();

    // Legacy single-profile key (read-only migration path)
    [JsonPropertyName("ExcelMapping")]
    public ExcelMappingDto? ExcelMapping { get; set; }
}

public class BexioSettingsDto
{
    [JsonPropertyName("ApiToken")]
    public string ApiToken { get; set; } = "bexio_api_token_here";
    [JsonPropertyName("AccountId")]
    public int? AccountId { get; set; } = null;
    [JsonPropertyName("TaxId")]
    public int? TaxId { get; set; } = null;
    [JsonPropertyName("Language")]
    public string Language { get; set; } = "de";
}

public class MappingProfileDto
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "Default";
    [JsonPropertyName("ExcelMapping")]
    public ExcelMappingDto ExcelMapping { get; set; } = new();
}

public class ExcelMappingDto
{
    [JsonPropertyName("WorksheetIndex")]
    public int WorksheetIndex { get; set; } = 1;
    [JsonPropertyName("PositionTextTemplate")]
    public string PositionTextTemplate { get; set; } = "Color: {Color}, Size: {Size}";
    [JsonPropertyName("Header")]
    public HeaderMappingDto Header { get; set; } = new();
    [JsonPropertyName("SizeMatrix")]
    public SizeMatrixDto SizeMatrix { get; set; } = new();
    [JsonPropertyName("Data")]
    public DataMappingDto Data { get; set; } = new();
}

public class HeaderMappingDto
{
    [JsonPropertyName("CompanyNameCell")] public string CompanyNameCell { get; set; } = "B4";
    [JsonPropertyName("StreetCell")] public string StreetCell { get; set; } = "B5";
    [JsonPropertyName("ZipCityCell")] public string ZipCityCell { get; set; } = "B6";
    [JsonPropertyName("BuyerEmailCell")] public string BuyerEmailCell { get; set; } = "E5";
    [JsonPropertyName("BuyerNameCell")] public string BuyerNameCell { get; set; } = "E4";
    [JsonPropertyName("DeliveryDateCell")] public string DeliveryDateCell { get; set; } = "T7";
    [JsonPropertyName("PaymentTermsCell")] public string PaymentTermsCell { get; set; } = "A9";
    [JsonPropertyName("DiscountCell")] public string DiscountCell { get; set; } = "V12";
}

public class SizeMatrixDto
{
    [JsonPropertyName("StartRow")] public int StartRow { get; set; } = 10;
    [JsonPropertyName("EndRow")] public int EndRow { get; set; } = 17;
    [JsonPropertyName("CategoryColumn")] public int CategoryColumn { get; set; } = 4;
    [JsonPropertyName("StartSizeColumn")] public int StartSizeColumn { get; set; } = 5;
    [JsonPropertyName("EndSizeColumn")] public int EndSizeColumn { get; set; } = 18;
}

public class DataMappingDto
{
    [JsonPropertyName("StartRow")] public int StartRow { get; set; } = 18;
    [JsonPropertyName("ArticleNumberColumn")] public int ArticleNumberColumn { get; set; } = 1;
    [JsonPropertyName("ArticleNameColumn")] public int ArticleNameColumn { get; set; } = 2;
    [JsonPropertyName("ColorColumn")] public int ColorColumn { get; set; } = 3;
    [JsonPropertyName("CategoryColumn")] public int CategoryColumn { get; set; } = 4;
    [JsonPropertyName("StartQtyColumn")] public int StartQtyColumn { get; set; } = 5;
    [JsonPropertyName("EndQtyColumn")] public int EndQtyColumn { get; set; } = 18;
    [JsonPropertyName("UnitPriceColumn")] public int UnitPriceColumn { get; set; } = 20;
}
