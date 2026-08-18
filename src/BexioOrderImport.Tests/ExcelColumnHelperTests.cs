using System.Text.Json;
using BexioOrderImport.Application.Helpers;
using BexioOrderImport.Application.Options;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class ExcelColumnHelperTests
{
    [Test]
    [Arguments(1, "A")]
    [Arguments(2, "B")]
    [Arguments(26, "Z")]
    [Arguments(27, "AA")]
    [Arguments(28, "AB")]
    [Arguments(52, "AZ")]
    [Arguments(53, "BA")]
    [Arguments(702, "ZZ")]
    [Arguments(703, "AAA")]
    public void IndexToColumnLetter_ShouldConvertCorrectly(int columnNumber, string expectedLetter)
    {
        ExcelColumnHelper.IndexToColumnLetter(columnNumber).Should().Be(expectedLetter);
    }

    [Test]
    [Arguments("A", 1)]
    [Arguments("b", 2)]
    [Arguments("Z", 26)]
    [Arguments("AA", 27)]
    [Arguments("ab", 28)]
    [Arguments("AZ", 52)]
    [Arguments("BA", 53)]
    [Arguments("ZZ", 702)]
    [Arguments("AAA", 703)]
    public void ColumnLetterToIndex_ShouldConvertCorrectly(string letter, int expectedIndex)
    {
        ExcelColumnHelper.ColumnLetterToIndex(letter).Should().Be(expectedIndex);
    }

    [Test]
    [Arguments("1", "A")]
    [Arguments("4", "D")]
    [Arguments("18", "R")]
    [Arguments("a", "A")]
    [Arguments("  b  ", "B")]
    [Arguments("AA", "AA")]
    [Arguments("", "Fallback")]
    public void NormalizeColumnLetter_ShouldNormalizeNumericAndStringValues(string input, string expected)
    {
        ExcelColumnHelper.NormalizeColumnLetter(input, "Fallback").Should().Be(expected);
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Testing JsonConverter with numeric token")]
    public void JsonConverter_ShouldDeserializeNumericAndStringTokens()
    {
        string jsonWithNumber = "{\"CategoryColumn\": 4}";
        var deserializedNumber = JsonSerializer.Deserialize<SizeMatrixMapping>(jsonWithNumber);
        deserializedNumber!.CategoryColumn.Should().Be("D");

        string jsonWithStringNumber = "{\"CategoryColumn\": \"4\"}";
        var deserializedStringNumber = JsonSerializer.Deserialize<SizeMatrixMapping>(jsonWithStringNumber);
        deserializedStringNumber!.CategoryColumn.Should().Be("D");

        string jsonWithLetter = "{\"CategoryColumn\": \"D\"}";
        var deserializedLetter = JsonSerializer.Deserialize<SizeMatrixMapping>(jsonWithLetter);
        deserializedLetter!.CategoryColumn.Should().Be("D");
    }
}
