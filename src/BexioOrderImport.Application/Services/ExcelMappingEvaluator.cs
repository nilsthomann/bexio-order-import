using System.Text.Json;
using System.Text.RegularExpressions;
using BexioOrderImport.Application.Helpers;
using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Application.Services;

public static class ExcelMappingEvaluator
{
    private static readonly Regex CellRegex = new(@"^[A-Za-z]+[1-9]\d*$", RegexOptions.Compiled);

    public static bool IsValidCellAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        return CellRegex.IsMatch(address.Trim());
    }

    public static bool IsValidColumnLetter(string? columnLetter)
    {
        return ExcelColumnHelper.IsValidColumnLetter(columnLetter);
    }

    public static void CopyOptions(ExcelMappingOptions source, ExcelMappingOptions target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var json = JsonSerializer.Serialize(source);
        var temp = JsonSerializer.Deserialize<ExcelMappingOptions>(json);
        if (temp != null)
        {
            target.WorksheetIndex = temp.WorksheetIndex;
            target.DefaultOrderName = temp.DefaultOrderName;
            target.SeasonCode = temp.SeasonCode;
            target.SinglePositionTextTemplate = temp.SinglePositionTextTemplate;
            target.GroupedPositionTextTemplate = temp.GroupedPositionTextTemplate;
            target.DiscountPositionTextTemplate = temp.DiscountPositionTextTemplate;
            target.SizeRowTemplate = temp.SizeRowTemplate;
            target.PositionGroupingMode = temp.PositionGroupingMode;
            target.Header = temp.Header;
            target.SizeMatrix = temp.SizeMatrix;
            target.Data = temp.Data;
        }
    }

    public static ExcelMappingOptions CloneOptions(ExcelMappingOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ExcelMappingOptions>(json) ?? new ExcelMappingOptions();
    }
}
