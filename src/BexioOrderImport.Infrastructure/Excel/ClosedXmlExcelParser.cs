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

        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheet(_options.WorksheetIndex);

        var order = new Order
        {
            // 1. Kopfdaten parsen
            Customer = ParseCustomerHeader(sheet),
            DeliveryDate = ParseDeliveryDate(sheet)
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

        // 2. Grössenmatrizen aus Zeilen 10–17 einlesen
        var sizeMatrices = ParseSizeMatrices(sheet);

        // 3. Zeilenweise Daten ab StartRow einlesen
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? _options.Data.StartRow;
        for (int r = _options.Data.StartRow; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            string artNr = row.Cell(_options.Data.ArticleNumberColumn).Value.ToString().Trim();
            string artName = row.Cell(_options.Data.ArticleNameColumn).Value.ToString().Trim();
            string color = row.Cell(_options.Data.ColorColumn).Value.ToString().Trim();
            string rawCategory = row.Cell(_options.Data.CategoryColumn).Value.ToString().Trim();

            // Abbruchbedingung bei Leerzeile
            if (string.IsNullOrEmpty(artNr) && string.IsNullOrEmpty(artName))
                continue;

            // Preis aus Spalte für EP auslesen
            _ = decimal.TryParse(row.Cell(_options.Data.UnitPriceColumn).Value.ToString(), out decimal unitPrice);

            // Matrix dynamisch zuordnen
            string? matchedCategory = MapCategoryName(rawCategory, sizeMatrices.Keys);
            if (matchedCategory == null || !sizeMatrices.ContainsKey(matchedCategory))
                continue; // Kategorie nicht in Matrix-Def vorhanden

            var sizes = sizeMatrices[matchedCategory];

            // Prüfe Grössen-Spalten auf Bestellmengen
            for (int col = _options.Data.StartQtyColumn; col <= _options.Data.EndQtyColumn; col++)
            {
                string qtyStr = row.Cell(col).Value.ToString();
                if (int.TryParse(qtyStr, out int qty) && qty > 0)
                {
                    string sizeName = sizes.TryGetValue(col, out string? value) ? value : $"Spalte_{col}";

                    order.Positions.Add(new OrderPosition
                    {
                        ArticleNumber = artNr,
                        ArticleName = artName,
                        Color = color,
                        SizeCategory = rawCategory,
                        Size = sizeName,
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        DiscountPercent = order.DiscountPercent
                    });
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

    private DateTime? ParseDeliveryDate(IXLWorksheet sheet)
    {
        string val = sheet.Cell(_options.Header.DeliveryDateCell).Value.ToString();
        if (DateTime.TryParse(val, out DateTime dt)) return dt;
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

        // 1. Exakter Match
        foreach (var reg in registeredCategories)
        {
            if (reg.Equals(rawCategory, StringComparison.OrdinalIgnoreCase))
                return reg;
        }

        // 2. Robustes Mapping
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

        // 3. Fallback auf StartsWith/Contains
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
