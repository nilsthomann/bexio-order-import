using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Abstractions.Formats;

namespace BexioOrderImport.Format.Peakmile;

public class PeakmileSettings : IFormatSettings, IJsonOnDeserialized
{
    private string _singlePositionTextTemplate = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}";
    private bool _hasSinglePositionTextTemplate;
    private string? _legacyPositionTextTemplate;

    public int WorksheetIndex { get; set; } = 1;
    public string DefaultOrderName { get; set; } = "Order: {CustomerName} {SeasonCode}";
    public string SeasonCode { get; set; } = "FS27";
    public List<string> SeasonFallbackCodes { get; set; } = [];

    public string SinglePositionTextTemplate
    {
        get => _singlePositionTextTemplate;
        set
        {
            _singlePositionTextTemplate = value;
            _hasSinglePositionTextTemplate = true;
        }
    }

    public string GroupedPositionTextTemplate { get; set; } = "<strong>{BexioArticleName}</strong><br />{SizesRows}<br />{BexioArticleDescription}";

    /// <summary>
    /// The position text template of the grouping mode.
    /// </summary>
    [JsonIgnore]
    public string PositionTextTemplate => PositionGroupingMode == PositionGroupingMode.GroupedSizePosition
        ? GroupedPositionTextTemplate
        : SinglePositionTextTemplate;

    public string DiscountPositionTextTemplate { get; set; } = "Rabatt ({DiscountInPercent}%)";
    public string SizeRowTemplate { get; set; } = "{Amount}x Size {Size}";
    public PositionGroupingMode PositionGroupingMode { get; set; } = PositionGroupingMode.SinglePositionPerSize;
    public HeaderMapping Header { get; set; } = new();
    public SizeMatrixMapping SizeMatrix { get; set; } = new();
    public DataMapping Data { get; set; } = new();

    // TODO: [Backward Compatibility] Remove in future version
    // Up to v1.3.0 profiles only stored "PositionTextTemplate". v1.4.0 to v1.6.0 also wrote it, but before the grouping mode,
    // so reading it back replaced the single template with the grouped one. It now only fills a missing single template.
    [JsonInclude]
    [JsonPropertyName("PositionTextTemplate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "Set by System.Text.Json when reading old profiles")]
    private string? LegacyPositionTextTemplate
    {
        get => null;
        set => _legacyPositionTextTemplate = value;
    }

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (_legacyPositionTextTemplate != null && !_hasSinglePositionTextTemplate)
        {
            _singlePositionTextTemplate = _legacyPositionTextTemplate;
        }
        _legacyPositionTextTemplate = null;
    }
}

public class HeaderMapping
{
    public string CompanyNameCell { get; set; } = "B4";
    public string StreetCell { get; set; } = "B5";
    public string ZipCityCell { get; set; } = "B6";
    public string BuyerEmailCell { get; set; } = "E5";
    public string BuyerNameCell { get; set; } = "E4";
    public bool EnableOrderNumber { get; set; }
    public string OrderNumberCell { get; set; } = "E6";
    public bool EnableCustomerNumber { get; set; }
    public string CustomerNumberCell { get; set; } = "E3";

    // TODO: [Backward Compatibility] Remove in future version
    // Names of profiles before v1.5.0. They are only read and set the new properties; the getters return the default,
    // so they are not written again.
    [JsonInclude]
    [JsonPropertyName("EnableOrderId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "Set by System.Text.Json when reading old profiles")]
    private bool LegacyEnableOrderId
    {
        get => false;
        set => EnableOrderNumber = value;
    }

    [JsonInclude]
    [JsonPropertyName("OrderIdCell")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "Set by System.Text.Json when reading old profiles")]
    private string? LegacyOrderIdCell
    {
        get => null;
        set => OrderNumberCell = value ?? OrderNumberCell;
    }

    [JsonInclude]
    [JsonPropertyName("EnableCustomerId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "Set by System.Text.Json when reading old profiles")]
    private bool LegacyEnableCustomerId
    {
        get => false;
        set => EnableCustomerNumber = value;
    }

    [JsonInclude]
    [JsonPropertyName("CustomerIdCell")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [SuppressMessage("CodeQuality", "IDE0051", Justification = "Set by System.Text.Json when reading old profiles")]
    private string? LegacyCustomerIdCell
    {
        get => null;
        set => CustomerNumberCell = value ?? CustomerNumberCell;
    }

    public string PaymentTermsCell { get; set; } = "A9";
    public string DiscountCell { get; set; } = "V12";
}

public class SizeMatrixMapping
{
    public int StartRow { get; set; } = 10;
    public int EndRow { get; set; } = 17;

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string CategoryColumn { get; set; } = "D";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string StartSizeColumn { get; set; } = "E";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string EndSizeColumn { get; set; } = "R";
}

public class DataMapping
{
    public int StartRow { get; set; } = 18;

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ArticleNumberColumn { get; set; } = "A";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ArticleNameColumn { get; set; } = "B";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ColorColumn { get; set; } = "C";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string CategoryColumn { get; set; } = "D";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string StartQtyColumn { get; set; } = "E";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string EndQtyColumn { get; set; } = "R";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string UnitPriceColumn { get; set; } = "T";

    public bool EnableRowDiscount { get; set; }

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string RowDiscountColumn { get; set; } = "U";
}
