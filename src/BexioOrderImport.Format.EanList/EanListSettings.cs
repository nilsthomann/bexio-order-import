using System.Text.Json.Serialization;
using BexioOrderImport.Abstractions.Excel;
using BexioOrderImport.Abstractions.Formats;

namespace BexioOrderImport.Format.EanList;

public class EanListSettings : IFormatSettings
{
    public int WorksheetIndex { get; set; } = 1;

    public int StartRow { get; set; } = 2;

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string EanColumn { get; set; } = "A";

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string QuantityColumn { get; set; } = "B";

    /// <summary>
    /// Reads the sales unit (VE) from SalesUnitColumn; without it every sales unit is 1.
    /// </summary>
    public bool UseSalesUnitColumn { get; set; }

    [JsonConverter(typeof(ExcelColumnJsonConverter))]
    public string SalesUnitColumn { get; set; } = "C";

    /// <summary>
    /// Bexio contact number (nr) of the customer. Each customer has its own profile.
    /// </summary>
    public string CustomerNumber { get; set; } = string.Empty;

    public string DefaultOrderName { get; set; } = "Order: {CustomerName}";

    public string PositionTextTemplate { get; set; } = "<strong>{BexioArticleName}</strong><br />{BexioArticleDescription}<br />Produktcode: {EAN}";
}
