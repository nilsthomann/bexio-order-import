using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Application.Helpers;

public static class ExcelColumnHelper
{
    private static readonly Regex ColumnLetterRegex = new(@"^[A-Za-z]+$", RegexOptions.Compiled);

    public static string IndexToColumnLetter(int columnNumber)
    {
        if (columnNumber <= 0) return string.Empty;
        string columnLetter = string.Empty;
        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnLetter = Convert.ToChar('A' + modulo) + columnLetter;
            columnNumber = (columnNumber - modulo) / 26;
        }
        return columnLetter;
    }

    public static int ColumnLetterToIndex(string columnLetter)
    {
        if (string.IsNullOrWhiteSpace(columnLetter)) return 0;
        columnLetter = columnLetter.Trim().ToUpperInvariant();
        int sum = 0;
        foreach (char c in columnLetter)
        {
            if (c < 'A' || c > 'Z') return 0;
            sum *= 26;
            sum += (c - 'A' + 1);
        }
        return sum;
    }

    public static bool IsNumeric(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return int.TryParse(input.Trim(), out _);
    }

    public static bool IsValidColumnLetter(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        string trimmed = input.Trim();
        if (IsNumeric(trimmed))
        {
            int num = int.Parse(trimmed);
            return num > 0;
        }
        return ColumnLetterRegex.IsMatch(trimmed);
    }

    public static string NormalizeColumnLetter(string? input, string fallback = "A")
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        string trimmed = input.Trim();
        if (int.TryParse(trimmed, out int num) && num > 0)
        {
            return IndexToColumnLetter(num);
        }
        string upper = trimmed.ToUpperInvariant();
        return ColumnLetterRegex.IsMatch(upper) ? upper : fallback;
    }

    /// <summary>
    /// Checks all column properties in mapping options. If any column value is numeric or un-normalized,
    /// converts it to an upper-case Excel column letter (e.g. 1 -> "A", 2 -> "B", 4 -> "D", 5 -> "E", etc.).
    /// Returns true if any column mapping was converted/updated.
    /// </summary>
    public static bool MigrateProfileColumnMappings(ExcelMappingOptions? mapping)
    {
        if (mapping == null) return false;
        bool migrated = false;

        migrated |= MigrateValue(mapping.SizeMatrix.CategoryColumn, "D", v => mapping.SizeMatrix.CategoryColumn = v);
        migrated |= MigrateValue(mapping.SizeMatrix.StartSizeColumn, "E", v => mapping.SizeMatrix.StartSizeColumn = v);
        migrated |= MigrateValue(mapping.SizeMatrix.EndSizeColumn, "R", v => mapping.SizeMatrix.EndSizeColumn = v);

        migrated |= MigrateValue(mapping.Data.ArticleNumberColumn, "A", v => mapping.Data.ArticleNumberColumn = v);
        migrated |= MigrateValue(mapping.Data.ArticleNameColumn, "B", v => mapping.Data.ArticleNameColumn = v);
        migrated |= MigrateValue(mapping.Data.ColorColumn, "C", v => mapping.Data.ColorColumn = v);
        migrated |= MigrateValue(mapping.Data.CategoryColumn, "D", v => mapping.Data.CategoryColumn = v);
        migrated |= MigrateValue(mapping.Data.StartQtyColumn, "E", v => mapping.Data.StartQtyColumn = v);
        migrated |= MigrateValue(mapping.Data.EndQtyColumn, "R", v => mapping.Data.EndQtyColumn = v);
        migrated |= MigrateValue(mapping.Data.UnitPriceColumn, "T", v => mapping.Data.UnitPriceColumn = v);
        migrated |= MigrateValue(mapping.Data.RowDiscountColumn, "U", v => mapping.Data.RowDiscountColumn = v);

        if (migrated)
        {
            ExcelColumnJsonConverter.HasPerformedNumericConversion = true;
        }

        return migrated;
    }

    private static bool MigrateValue(string currentValue, string fallback, Action<string> setter)
    {
        string normalized = NormalizeColumnLetter(currentValue, fallback);
        if (currentValue != normalized)
        {
            setter(normalized);
            return true;
        }
        return false;
    }
}

public class ExcelColumnJsonConverter : JsonConverter<string>
{
    [ThreadStatic]
    private static bool _hasPerformedNumericConversion;

    public static bool HasPerformedNumericConversion
    {
        get => _hasPerformedNumericConversion;
        set => _hasPerformedNumericConversion = value;
    }

    public static void ResetMigrationTracker()
    {
        _hasPerformedNumericConversion = false;
    }

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            _hasPerformedNumericConversion = true;
            if (reader.TryGetInt32(out int colNum) && colNum > 0)
            {
                return ExcelColumnHelper.IndexToColumnLetter(colNum);
            }
            return "A";
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? val = reader.GetString();
            if (!string.IsNullOrWhiteSpace(val))
            {
                if (int.TryParse(val.Trim(), out int num) && num > 0)
                {
                    _hasPerformedNumericConversion = true;
                    return ExcelColumnHelper.IndexToColumnLetter(num);
                }
                return val.Trim().ToUpperInvariant();
            }
        }

        return string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        string normalized = ExcelColumnHelper.NormalizeColumnLetter(value);
        writer.WriteStringValue(normalized);
    }
}
