using System.Globalization;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Wpf.Helpers;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class MissingArticleTableTests
{
    private static (string Text, string TabSeparated) FormatInEnglish(params MissingArticle[] missing)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            return MissingArticleTable.Format(missing);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Test]
    public void Format_ShouldAlignTheColumnsAndSeparateThemByTabsForExcel()
    {
        var (text, tabSeparated) = FormatInEnglish(
            new MissingArticle { Row = 3, ArticleNumber = "E183801", Color = "1001 Black", SeasonCode = "FS26", Size = "M", Quantity = 3 },
            new MissingArticle { Row = 12, ArticleNumber = "E1", Color = "Red", SeasonCode = "FS26", Size = "1x S<br />2x M", Quantity = 3, CandidateCount = 2 });

        tabSeparated.Should().Be(string.Join(Environment.NewLine,
            "Row\tArticle no.\tColor\tSeason\tSize\tQuantity\tReason",
            "3\tE183801\t1001 Black\tFS26\tM\t3\tnot found",
            "12\tE1\tRed\tFS26\t1x S, 2x M\t3\tnot unique (2)"));
        string[] lines = text.Split(Environment.NewLine);
        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("Row  Article no.  Color       Season  Size        Quantity  Reason");
        lines[1].IndexOf("not found", StringComparison.Ordinal).Should().Be(lines[0].IndexOf("Reason", StringComparison.Ordinal));
        lines[2].IndexOf("Red", StringComparison.Ordinal).Should().Be(lines[0].IndexOf("Color", StringComparison.Ordinal));
        lines.Should().OnlyContain(l => l == l.TrimEnd());
    }

    [Test]
    public void Format_ShouldLeaveOutTheColumnsThatAreEmptyInEveryRow()
    {
        var (_, tabSeparated) = FormatInEnglish(new MissingArticle { Ean = "7612345678900", Quantity = 12 });

        tabSeparated.Should().Be($"EAN\tQuantity\tReason{Environment.NewLine}7612345678900\t12\tnot found");
    }
}
