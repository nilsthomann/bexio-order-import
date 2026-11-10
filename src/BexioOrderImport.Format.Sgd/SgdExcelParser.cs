using System.Globalization;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Sgd.Resources;
using ClosedXML.Excel;

namespace BexioOrderImport.Format.Sgd;

/// <summary>
/// Reads an SGD list: one row per article, color, size and season with its quantity. The customer is asked for on import.
/// </summary>
public sealed class SgdExcelParser
{
    private readonly SgdSettings _settings;

    public SgdExcelParser(SgdSettings settings)
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

        var order = new Order();
        var rows = new List<OrderPosition>();
        var seasonCodes = SeasonCodesByName();
        // Rows of each season name without mapping, reported together so the profile can be completed at once
        var unknownSeasons = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? _settings.StartRow;
        for (int row = _settings.StartRow; row <= lastRow; row++)
        {
            string articleNumber = ReadText(sheet.Cell(row, _settings.ArticleNumberColumn));
            var quantityCell = sheet.Cell(row, _settings.QuantityColumn);
            if (articleNumber.Length == 0 && quantityCell.IsEmpty())
            {
                continue;
            }

            AddExternalOrderNumber(order, sheet, row);
            int? quantity = ReadWholeNumber(quantityCell, row);
            if (quantity is null or <= 0)
            {
                order.SkippedRows++;
                continue;
            }

            string color = ReadText(sheet.Cell(row, _settings.ColorColumn));
            if (articleNumber.Length == 0)
                throw new FormatException(string.Format(SgdTranslations.Error_MissingArticleNumber, row));
            if (color.Length == 0)
                throw new FormatException(string.Format(SgdTranslations.Error_MissingColor, row));

            string season = ReadText(sheet.Cell(row, _settings.SeasonColumn));
            if (season.Length == 0)
                throw new FormatException(string.Format(SgdTranslations.Error_MissingSeason, row));
            if (!seasonCodes.TryGetValue(season, out string? seasonCode))
            {
                if (!unknownSeasons.TryGetValue(season, out var unknownRows))
                {
                    unknownSeasons[season] = unknownRows = [];
                }
                unknownRows.Add(row);
                continue;
            }

            rows.Add(new OrderPosition
            {
                ArticleNumber = articleNumber,
                ArticleName = ReadText(sheet.Cell(row, _settings.ArticleNameColumn)),
                SeasonCode = seasonCode,
                Color = color,
                Size = ReadText(sheet.Cell(row, _settings.SizeColumn)),
                Quantity = quantity.Value,
                SourceRow = row
            });
        }

        if (unknownSeasons.Count > 0)
        {
            var lines = unknownSeasons.Select(s => string.Format(SgdTranslations.Error_UnknownSeasonRows, s.Key, string.Join(", ", s.Value)));
            throw new FormatException(SgdTranslations.Error_UnknownSeasons + Environment.NewLine + string.Join(Environment.NewLine, lines));
        }

        order.Positions = _settings.PositionGroupingMode == SgdPositionGroupingMode.GroupedSizePosition ? GroupBySize(rows) : rows;
        return order;
    }

    /// <summary>
    /// Bexio season code by SGD season name, ignoring case and surrounding whitespace.
    /// </summary>
    private Dictionary<string, string> SeasonCodesByName()
    {
        var codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in _settings.SeasonMappings ?? [])
        {
            codes.TryAdd(mapping.ExternalSeason?.Trim() ?? string.Empty, mapping.SeasonCode?.Trim() ?? string.Empty);
        }
        return codes;
    }

    /// <summary>
    /// One position per article, color and season in the order of their first row, with one size row per size (equal sizes are summed).
    /// </summary>
    private List<OrderPosition> GroupBySize(List<OrderPosition> rows)
    {
        string sizeRowTemplate = string.IsNullOrWhiteSpace(_settings.SizeRowTemplate) ? SgdSettings.DefaultSizeRowTemplate : _settings.SizeRowTemplate;

        return [.. rows
            .GroupBy(p => (p.ArticleNumber.ToUpperInvariant(), p.Color.ToUpperInvariant(), p.SeasonCode.ToUpperInvariant()))
            .Select(article =>
            {
                var sizeRows = article
                    .GroupBy(p => p.Size, StringComparer.OrdinalIgnoreCase)
                    .Select(size => sizeRowTemplate
                        .Replace("{Amount}", size.Sum(p => p.Quantity).ToString(CultureInfo.InvariantCulture))
                        .Replace("{Size}", size.Key));
                var first = article.First();
                return new OrderPosition
                {
                    ArticleNumber = first.ArticleNumber,
                    ArticleName = first.ArticleName,
                    SeasonCode = first.SeasonCode,
                    Color = first.Color,
                    Size = string.Join("<br />", sizeRows),
                    Quantity = article.Sum(p => p.Quantity),
                    SourceRow = first.SourceRow
                };
            })];
    }

    /// <summary>
    /// Adds the external order number of the row unless it is empty or already known.
    /// </summary>
    private void AddExternalOrderNumber(Order order, IXLWorksheet sheet, int row)
    {
        if (!_settings.UseExternalOrderNumberColumn) return;

        string number = ReadText(sheet.Cell(row, _settings.ExternalOrderNumberColumn));
        if (number.Length > 0 && !order.ExternalOrderNumbers.Contains(number, StringComparer.OrdinalIgnoreCase))
        {
            order.ExternalOrderNumbers.Add(number);
        }
    }

    /// <summary>
    /// Trimmed cell text. Numbers are written out in full (Excel shows long numbers as eg. 1.2E+5) and keep their decimals (size 38.5).
    /// </summary>
    private static string ReadText(IXLCell cell) => cell.DataType == XLDataType.Number
        ? cell.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture)
        : cell.Value.ToString().Trim();

    /// <summary>
    /// Null for an empty cell. Decimals and text that isn't a whole number are invalid.
    /// </summary>
    private static int? ReadWholeNumber(IXLCell cell, int row)
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

        throw new FormatException(string.Format(SgdTranslations.Error_InvalidQuantity, cell.Value.ToString().Trim(), row));
    }
}
