using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Domain.Models;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;

namespace BexioOrderImport.Infrastructure.Excel;

public class ClosedXmlExcelParser : IExcelParser
{
    private readonly ExcelMappingOptions _options;

    public ClosedXmlExcelParser(IOptions<ExcelMappingOptions> options)
    {
        _options = options.Value;
    }

    public Order ParseOrderForm(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel file not found: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(_options.WorksheetIndex);

        var order = new Order
        {
            // 1. Parse header data
            Customer = ParseCustomerHeader(sheet),
            OrderId = ParseOrderId(sheet)
        };

        string paymentTermsVal = sheet.Cell(_options.Header.PaymentTermsCell).Value.ToString().Trim();
        order.PaymentTerms = paymentTermsVal;

        // Parse discount
        string discountVal = sheet.Cell(_options.Header.DiscountCell).Value.ToString().Trim();
        discountVal = discountVal.Replace("%", "").Trim();
        if (decimal.TryParse(discountVal, out decimal parsedDiscount))
        {
            if (parsedDiscount > 0 && parsedDiscount < 1)
            {
                parsedDiscount *= 100;
            }
            order.DiscountPercent = parsedDiscount;
        }

        // 2. Read size matrices from rows 10-17
        var sizeMatrices = ParseSizeMatrices(sheet);

        // 3. Read row data starting from StartRow
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? _options.Data.StartRow;
        for (int r = _options.Data.StartRow; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            string artNr = row.Cell(_options.Data.ArticleNumberColumn).Value.ToString().Trim();
            string artName = row.Cell(_options.Data.ArticleNameColumn).Value.ToString().Trim();
            string color = row.Cell(_options.Data.ColorColumn).Value.ToString().Trim();
            string rawCategory = row.Cell(_options.Data.CategoryColumn).Value.ToString().Trim();

            // Stop condition on empty row
            if (string.IsNullOrEmpty(artNr) && string.IsNullOrEmpty(artName))
                continue;

            // Read unit price from column
            _ = decimal.TryParse(row.Cell(_options.Data.UnitPriceColumn).Value.ToString(), out decimal unitPrice);

            // Dynamically assign matrix
            string? matchedCategory = MapCategoryName(rawCategory, sizeMatrices.Keys);
            if (matchedCategory == null || !sizeMatrices.ContainsKey(matchedCategory))
                continue; // Category not present in matrix definitions

            var sizes = sizeMatrices[matchedCategory];

            // Check size columns for order quantities
            for (int col = _options.Data.StartQtyColumn; col <= _options.Data.EndQtyColumn; col++)
            {
                string qtyStr = row.Cell(col).Value.ToString();
                if (int.TryParse(qtyStr, out int qty) && qty > 0)
                {
                    string sizeName = sizes.TryGetValue(col, out string? value) ? value : $"Col_{col}";

                    var pos = new OrderPosition
                    {
                        ArticleNumber = artNr,
                        ArticleName = artName,
                        Color = color,
                        SizeCategory = rawCategory,
                        Size = sizeName,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        DiscountPercent = order.DiscountPercent
                    };
                    order.Positions.Add(pos);
                }
            }
        }

        return order;
    }

    private Customer ParseCustomerHeader(IXLWorksheet sheet)
    {
        string companyVal = sheet.Cell(_options.Header.CompanyNameCell).Value.ToString().Trim();
        string streetVal = sheet.Cell(_options.Header.StreetCell).Value.ToString().Trim();
        string zipCityVal = sheet.Cell(_options.Header.ZipCityCell).Value.ToString().Trim();
        string emailVal = sheet.Cell(_options.Header.BuyerEmailCell).Value.ToString().Trim();
        string buyerVal = sheet.Cell(_options.Header.BuyerNameCell).Value.ToString().Trim();

        return new Customer
        {
            CompanyName = companyVal,
            Street = streetVal,
            ZipCode = ExtractZip(zipCityVal),
            City = ExtractCity(zipCityVal),
            Email = emailVal,
            BuyerName = buyerVal
        };
    }

    private int? ParseOrderId(IXLWorksheet sheet)
    {
        string val = sheet.Cell(_options.Header.OrderIdCell).Value.ToString().Trim();
        if (int.TryParse(val, out int id)) return id;
        return null;
    }

    private Dictionary<string, Dictionary<int, string>> ParseSizeMatrices(IXLWorksheet sheet)
    {
        var matrices = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        var matrixOpt = _options.SizeMatrix;

        for (int r = matrixOpt.StartRow; r <= matrixOpt.EndRow; r++)
        {
            var row = sheet.Row(r);
            string categoryName = row.Cell(matrixOpt.CategoryColumn).Value.ToString().Trim();
            if (string.IsNullOrEmpty(categoryName)) continue;

            var columns = new Dictionary<int, string>();
            for (int col = matrixOpt.StartSizeColumn; col <= matrixOpt.EndSizeColumn; col++)
            {
                string sizeVal = row.Cell(col).Value.ToString().Trim();
                if (!string.IsNullOrEmpty(sizeVal))
                {
                    columns[col] = sizeVal;
                }
            }
            matrices[categoryName] = columns;
        }
        return matrices;
    }

    private static string? MapCategoryName(string rawCategory, IEnumerable<string> registeredCategories)
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return null;

        // 1. Exact match
        foreach (var reg in registeredCategories)
        {
            if (reg.Equals(rawCategory, StringComparison.OrdinalIgnoreCase))
                return reg;
        }

        // 2. Category mapping rules
        if (rawCategory.Contains("Hat", StringComparison.OrdinalIgnoreCase) ||
            rawCategory.Contains("Neck", StringComparison.OrdinalIgnoreCase))
            return "Hats/Necks";

        if (rawCategory.Contains("Mitten", StringComparison.OrdinalIgnoreCase) ||
            rawCategory.Contains("Acc", StringComparison.OrdinalIgnoreCase))
            return "Mittens/Acc";

        if (rawCategory.Contains("Socks", StringComparison.OrdinalIgnoreCase) ||
            rawCategory.Contains("UWear", StringComparison.OrdinalIgnoreCase))
            return "Socks/UWear";

        if (rawCategory.Contains("Shoes 32", StringComparison.OrdinalIgnoreCase))
            return "Shoes 32-42";

        if (rawCategory.Contains("Shoes 20", StringComparison.OrdinalIgnoreCase))
            return "Shoes 20-31";

        // 3. Fallback to StartsWith/Contains
        foreach (var reg in registeredCategories)
        {
            if (reg.StartsWith(rawCategory, StringComparison.OrdinalIgnoreCase) ||
                rawCategory.StartsWith(reg, StringComparison.OrdinalIgnoreCase))
                return reg;
        }

        return null;
    }

    private static string ExtractZip(string rawZipCity)
    {
        var parts = rawZipCity.Split(' ', 2);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string ExtractCity(string rawZipCity)
    {
        var parts = rawZipCity.Split(' ', 2);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }

}
