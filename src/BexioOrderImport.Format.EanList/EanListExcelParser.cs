using System.Globalization;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.EanList.Resources;
using ClosedXML.Excel;
using Nager.ArticleNumber;

namespace BexioOrderImport.Format.EanList;

/// <summary>
/// Reads an EAN list: one position per row with EAN and quantity. The customer number comes from the profile.
/// </summary>
public sealed class EanListExcelParser
{
    private readonly EanListSettings _settings;

    public EanListExcelParser(EanListSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public Order ParseOrderForm(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel file not found: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(_settings.WorksheetIndex);

        // Without an order number the importer creates a new order
        var order = new Order { CustomerNumber = _settings.CustomerNumber };

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? _settings.StartRow;
        for (int row = _settings.StartRow; row <= lastRow; row++)
        {
            var eanCell = sheet.Cell(row, _settings.EanColumn);
            var quantityCell = sheet.Cell(row, _settings.QuantityColumn);
            if (eanCell.IsEmpty() && quantityCell.IsEmpty())
            {
                continue;
            }

            int? quantity = ReadWholeNumber(quantityCell, row, EanListTranslations.Error_InvalidQuantity);
            if (quantity is null or <= 0)
            {
                order.SkippedRows++;
                continue;
            }

            order.Positions.Add(new OrderPosition
            {
                Ean = ReadEan(eanCell, row),
                Quantity = quantity.Value,
                SalesUnit = _settings.UseSalesUnitColumn ? ReadSalesUnit(sheet.Cell(row, _settings.SalesUnitColumn), row) : 1,
                SourceRow = row
            });
        }

        return order;
    }

    /// <summary>
    /// An empty cell is the sales unit 1. Other values must be whole numbers greater than 0.
    /// </summary>
    private static int ReadSalesUnit(IXLCell cell, int row)
    {
        int salesUnit = ReadWholeNumber(cell, row, EanListTranslations.Error_InvalidSalesUnit) ?? 1;
        return salesUnit > 0
            ? salesUnit
            : throw new FormatException(string.Format(EanListTranslations.Error_InvalidSalesUnit, cell.Value.ToString().Trim(), row));
    }

    /// <summary>
    /// Null for an empty cell. Decimals and text that isn't a whole number are invalid.
    /// </summary>
    /// <param name="errorFormat">Message with the cell value ({0}) and the row ({1}).</param>
    private static int? ReadWholeNumber(IXLCell cell, int row, string errorFormat)
    {
        if (cell.IsEmpty()) return null;

        if (cell.DataType == XLDataType.Number)
        {
            double number = cell.GetDouble();
            if (number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue)
            {
                return (int)number;
            }
        }
        else
        {
            string text = cell.Value.ToString().Trim();
            if (text.Length == 0) return null;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }
        }

        throw new FormatException(string.Format(errorFormat, cell.Value.ToString().Trim(), row));
    }

    /// <summary>
    /// Reads a valid EAN-13. EANs stored as numbers are written out in full (Excel shows long numbers as eg. 7.61234E+12).
    /// Shorter codes get the leading zeros back that Excel drops from number cells (826710896036 is the EAN 0826710896036).
    /// </summary>
    private static string ReadEan(IXLCell cell, int row)
    {
        string ean = cell.DataType == XLDataType.Number
            ? cell.GetDouble().ToString("0", CultureInfo.InvariantCulture)
            : cell.Value.ToString().Replace(" ", string.Empty).Trim();

        if (ean.Length == 0)
        {
            throw new FormatException(string.Format(EanListTranslations.Error_MissingEan, row));
        }

        string ean13 = ean.PadLeft(13, '0');
        if (!ean13.All(char.IsAsciiDigit) || ArticleNumberHelper.GetArticleNumberType(ean13) != ArticleNumberType.EAN13)
        {
            throw new FormatException(string.Format(EanListTranslations.Error_InvalidEan, ean, row));
        }
        return ean13;
    }
}
