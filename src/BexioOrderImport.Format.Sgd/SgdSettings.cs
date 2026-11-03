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
    /// Bexio intern_code of an article: {SeasonCode}{ArticleNumber}{Color}.
    /// </summary>
    public string SeasonCode { get; set; } = "FS27";

    /// <summary>
    /// Season codes tried in order when the season code finds no article (multi-season articles, eg. NOOS).
    /// </summary>
    public List<string> SeasonFallbackCodes { get; set; } = [];

    /// <summary>
    /// Discount in percent, added as a discount position at the end of the order. 0 adds none.
    /// </summary>
    public decimal DiscountPercent { get; set; }

    public SgdPositionGroupingMode PositionGroupingMode { get; set; } = SgdPositionGroupingMode.SinglePositionPerSize;

    public string DefaultOrderName { get; set; } = "Order: {CustomerName} {SeasonCode}";

    public string SinglePositionTextTemplate { get; set; } = "<strong>{BexioArticleName} Size {Size}</strong><br />{BexioArticleDescription}";

    public string GroupedPositionTextTemplate { get; set; } = "<strong>{BexioArticleName}</strong><br />{SizesRows}<br />{BexioArticleDescription}";

    public string SizeRowTemplate { get; set; } = DefaultSizeRowTemplate;

    public string DiscountPositionTextTemplate { get; set; } = "Rabatt ({DiscountInPercent}%)";
}
