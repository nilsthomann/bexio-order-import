using System.Text.Json.Serialization;
using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Abstractions.Formats;

namespace BexioOrderImport.Format.EanList;

public class EanListSettings : IFormatSettings
{
    public int WorksheetIndex { get; set; } = 1;

    /// <summary>
    /// First data row. The customer and order number are read from this row only.
    /// </summary>
    public int StartRow { get; set; } = 2;

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string EanColumn { get; set; } = "A";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string QuantityColumn { get; set; } = "B";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string CustomerNumberColumn { get; set; } = "C";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string OrderNumberColumn { get; set; } = "D";

    public string DefaultOrderName { get; set; } = "Order: {CustomerName}";

    public string PositionTextTemplate { get; set; } = "<strong>{BexioArticleName}</strong><br />{BexioArticleDescription}";
}
