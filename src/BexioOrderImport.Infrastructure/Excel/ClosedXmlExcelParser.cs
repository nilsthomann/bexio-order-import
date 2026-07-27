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
            // Parse header data
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

        // Read size matrices
        var sizeMatrices = ParseSizeMatrices(sheet);

        // Read row data starting from StartRow
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? _options.Data.StartRow;
        for (int r = _options.Data.StartRow; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            string artNr = row.Cell(_options.Data.ArticleNumberColumn).Value.ToString().Trim();
            string artName = row.Cell(_options.Data.ArticleNameColumn).Value.ToString().Trim();
            string color = row.Cell(_options.Data.ColorColumn).Value.ToString().Trim();
            string rawCategory = row.Cell(_options.Data.CategoryColumn).Value.ToString().Trim();

            // Stop condition on empty row or empty category
            if (string.IsNullOrEmpty(artNr) && string.IsNullOrEmpty(artName))
                continue;

            if (string.IsNullOrEmpty(rawCategory))
                continue;

            // Read unit price from column
            string priceStr = row.Cell(_options.Data.UnitPriceColumn).Value.ToString().Trim();
            if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal unitPrice) &&
                !decimal.TryParse(priceStr, out unitPrice))
            {
                if (!string.IsNullOrWhiteSpace(priceStr))
                {
                    throw new FormatException($"Ungültiges Preisformat '{priceStr}' in Zeile {r}, Spalte {_options.Data.UnitPriceColumn}.");
                }
                unitPrice = 0m;
            }

            // Read row-based discount if enabled
            decimal? rowDiscount = null;
            if (_options.Data.EnableRowDiscount)
            {
                string discountStr = row.Cell(_options.Data.RowDiscountColumn).Value.ToString().Replace("%", "").Trim();
                if (decimal.TryParse(discountStr, out decimal parsedRowDiscount))
                {
                    if (parsedRowDiscount > 0 && parsedRowDiscount < 1)
                    {
                        parsedRowDiscount *= 100;
                    }
                    rowDiscount = parsedRowDiscount;
                }
            }

            // Dynamically assign matrix
            if (!sizeMatrices.TryGetValue(rawCategory, out var sizes))
            {
                throw new InvalidOperationException($"Category '{rawCategory}' on row {r} is not defined in the size matrix.");
            }

            // Check size columns for order quantities
            for (int col = _options.Data.StartQtyColumn; col <= _options.Data.EndQtyColumn; col++)
            {
                string qtyStr = row.Cell(col).Value.ToString();
                if (int.TryParse(qtyStr, out int qty) && qty > 0)
                {
                    if (!sizes.TryGetValue(col, out string? sizeName) || string.IsNullOrWhiteSpace(sizeName))
                    {
                        throw new InvalidOperationException($"Size header for column {col} in category '{rawCategory}' (row {r}) was not defined in the size matrix.");
                    }

                    var pos = new OrderPosition
                    {
                        ArticleNumber = artNr,
                        ArticleName = artName,
                        Color = color,
                        SizeCategory = rawCategory,
                        Size = sizeName,
                        Quantity = qty,
                        GrossUnitPrice = unitPrice,
                        DiscountPercent = rowDiscount
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
        var (zip, city) = ExtractZipAndCity(zipCityVal);
        string emailVal = sheet.Cell(_options.Header.BuyerEmailCell).Value.ToString().Trim();
        string buyerVal = sheet.Cell(_options.Header.BuyerNameCell).Value.ToString().Trim();

        return new Customer
        {
            CompanyName = companyVal,
            Street = streetVal,
            ZipCode = zip,
            City = city,
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
            var subCategories = categoryName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var subCat in subCategories)
            {
                matrices[subCat] = columns;
                if (subCat.StartsWith("Shoes 32", StringComparison.OrdinalIgnoreCase))
                {
                    matrices["Shoes 32"] = columns;
                    matrices["Shoes 32-41"] = columns;
                    matrices["Shoes 32-42"] = columns;
                }
                else if (subCat.StartsWith("Shoes 20", StringComparison.OrdinalIgnoreCase))
                {
                    matrices["Shoes 20"] = columns;
                    matrices["Shoes 20-31"] = columns;
                    matrices["Shoes 20-32"] = columns;
                }
            }
        }
        return matrices;
    }

    internal static (string Zip, string City) ExtractZipAndCity(string rawZipCity)
    {
        if (string.IsNullOrWhiteSpace(rawZipCity)) return (string.Empty, string.Empty);
        var parts = rawZipCity.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1].Trim())
        };
    }
}
