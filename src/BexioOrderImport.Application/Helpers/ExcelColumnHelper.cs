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
}

public class ExcelColumnJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
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
