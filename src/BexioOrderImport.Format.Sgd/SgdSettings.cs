using System.Text.Json.Serialization;
using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Abstractions.Formats;

namespace BexioOrderImport.Format.Sgd;

public enum SgdPositionGroupingMode
{
    /// <summary>
    /// One Bexio position per row (size).
    /// </summary>
    SinglePositionPerSize = 0,

    /// <summary>
    /// One Bexio position per article and color with a text row per size.
    /// </summary>
    GroupedSizePosition = 1
}

/// <summary>
/// Profile settings of SGD lists. The customer and the order are asked for on import, so one profile serves all customers.
/// </summary>
public class SgdSettings : IFormatSettings
{
    public const string DefaultSizeRowTemplate = "{Amount}x Size {Size}";

    public int WorksheetIndex { get; set; } = 1;

    public int StartRow { get; set; } = 2;

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ArticleNumberColumn { get; set; } = "A";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ArticleNameColumn { get; set; } = "B";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ColorColumn { get; set; } = "C";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string SizeColumn { get; set; } = "D";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string QuantityColumn { get; set; } = "E";

    /// <summary>
    /// Reads the external order number from ExternalOrderNumberColumn for the preview and the {ExternalOrderNumber} placeholder.
    /// </summary>
    public bool UseExternalOrderNumberColumn { get; set; }

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string ExternalOrderNumberColumn { get; set; } = "F";

    /// <summary>
    /// Season of each row as named by SGD (eg. "2026 Fall/Winter"), mapped to a Bexio season code with SeasonMappings.
    /// </summary>
    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string SeasonColumn { get; set; } = "G";

    /// <summary>
    /// SGD season names and their Bexio season codes. The Bexio intern_code of an article is {SeasonCode}{ArticleNumber}{Color}.
    /// </summary>
    public List<SgdSeasonMapping> SeasonMappings { get; set; } = [];

    /// <summary>
    /// Discount in percent, added as a discount position at the end of the order. 0 adds none.
    /// </summary>
    public decimal DiscountPercent { get; set; }

    public SgdPositionGroupingMode PositionGroupingMode { get; set; } = SgdPositionGroupingMode.SinglePositionPerSize;

    public string DefaultOrderName { get; set; } = "Order: {CustomerName}";

    public string SinglePositionTextTemplate { get; set; } = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}";

    public string GroupedPositionTextTemplate { get; set; } = "<strong>{BexioArticleName}</strong><br />{SizesRows}<br />{BexioArticleDescription}";

    public string SizeRowTemplate { get; set; } = DefaultSizeRowTemplate;

    public string DiscountPositionTextTemplate { get; set; } = "Rabatt ({DiscountInPercent}%)";
}

/// <summary>
/// An SGD season name (eg. "2026 Fall/Winter") and its Bexio season code (eg. "HW26").
/// </summary>
public class SgdSeasonMapping
{
    public string ExternalSeason { get; set; } = string.Empty;

    public string SeasonCode { get; set; } = string.Empty;
}
