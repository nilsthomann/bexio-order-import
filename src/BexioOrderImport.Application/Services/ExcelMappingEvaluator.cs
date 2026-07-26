using System.Text.RegularExpressions;
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

    public static void CopyOptions(ExcelMappingOptions source, ExcelMappingOptions target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.WorksheetIndex = source.WorksheetIndex;
        target.DefaultOrderName = source.DefaultOrderName;
        target.SeasonCode = source.SeasonCode;
        target.PositionTextTemplate = source.PositionTextTemplate;
        target.DiscountPositionTextTemplate = source.DiscountPositionTextTemplate;

        target.Header.CompanyNameCell = source.Header.CompanyNameCell;
        target.Header.StreetCell = source.Header.StreetCell;
        target.Header.ZipCityCell = source.Header.ZipCityCell;
        target.Header.BuyerEmailCell = source.Header.BuyerEmailCell;
        target.Header.BuyerNameCell = source.Header.BuyerNameCell;
        target.Header.OrderIdCell = source.Header.OrderIdCell;
        target.Header.PaymentTermsCell = source.Header.PaymentTermsCell;
        target.Header.DiscountCell = source.Header.DiscountCell;

        target.SizeMatrix.StartRow = source.SizeMatrix.StartRow;
        target.SizeMatrix.EndRow = source.SizeMatrix.EndRow;
        target.SizeMatrix.CategoryColumn = source.SizeMatrix.CategoryColumn;
        target.SizeMatrix.StartSizeColumn = source.SizeMatrix.StartSizeColumn;
        target.SizeMatrix.EndSizeColumn = source.SizeMatrix.EndSizeColumn;

        target.Data.StartRow = source.Data.StartRow;
        target.Data.ArticleNumberColumn = source.Data.ArticleNumberColumn;
        target.Data.ArticleNameColumn = source.Data.ArticleNameColumn;
        target.Data.ColorColumn = source.Data.ColorColumn;
        target.Data.CategoryColumn = source.Data.CategoryColumn;
        target.Data.StartQtyColumn = source.Data.StartQtyColumn;
        target.Data.EndQtyColumn = source.Data.EndQtyColumn;
        target.Data.UnitPriceColumn = source.Data.UnitPriceColumn;
        target.Data.EnableRowDiscount = source.Data.EnableRowDiscount;
        target.Data.RowDiscountColumn = source.Data.RowDiscountColumn;
    }

    public static ExcelMappingOptions CloneOptions(ExcelMappingOptions source)
    {
        var clone = new ExcelMappingOptions();
        CopyOptions(source, clone);
        return clone;
    }
}
