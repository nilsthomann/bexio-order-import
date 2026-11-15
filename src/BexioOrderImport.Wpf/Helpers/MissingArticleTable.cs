using System.Globalization;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Helpers;

/// <summary>
/// The missing articles of an import as a table: <c>Text</c> with aligned columns for the dialog and <c>TabSeparated</c>
/// to paste into Excel. A column that is empty in every row is left out.
/// </summary>
public static class MissingArticleTable
{
    public static (string Text, string TabSeparated) Format(IReadOnlyList<MissingArticle> missing)
    {
        (string Header, Func<MissingArticle, string?> Value)[] columns =
        [
            (Translations.MissingArticles_Row, m => m.Row > 0 ? m.Row.ToString(CultureInfo.CurrentCulture) : null),
            (Translations.MissingArticles_Ean, m => m.Ean),
            (Translations.MissingArticles_ArticleNumber, m => m.ArticleNumber),
            (Translations.MissingArticles_Color, m => m.Color),
            (Translations.MissingArticles_Season, m => m.SeasonCode),
            // Grouped positions keep their size rows as HTML lines
            (Translations.MissingArticles_Size, m => m.Size?.Replace("<br />", ", ", StringComparison.Ordinal)),
            (Translations.MissingArticles_Quantity, m => m.Quantity.ToString(CultureInfo.CurrentCulture)),
            (Translations.MissingArticles_Reason, m => m.CandidateCount > 1
                ? string.Format(CultureInfo.CurrentCulture, Translations.MissingArticles_NotUnique, m.CandidateCount)
                : Translations.MissingArticles_NotFound)
        ];

        var shown = columns
            .Select(c => (c.Header, Values: missing.Select(m => c.Value(m)?.Trim() ?? string.Empty).ToList()))
            .Where(c => c.Values.Any(v => v.Length > 0))
            .ToList();
        List<List<string>> lines = [[.. shown.Select(c => c.Header)], .. missing.Select((_, row) => shown.Select(c => c.Values[row]).ToList())];
        int[] widths = [.. shown.Select((_, column) => lines.Max(l => l[column].Length))];

        string text = string.Join(Environment.NewLine, lines.Select(l => string.Join("  ", l.Select((v, column) => v.PadRight(widths[column]))).TrimEnd()));
        string tabSeparated = string.Join(Environment.NewLine, lines.Select(l => string.Join('\t', l)));
        return (text, tabSeparated);
    }
}
