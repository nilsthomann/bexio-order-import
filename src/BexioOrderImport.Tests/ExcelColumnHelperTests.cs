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
    [Arguments(0, "")]
    [Arguments(-5, "")]
    public void IndexToColumnLetter_WithInvalidNumber_ShouldReturnEmptyString(int columnNumber, string expectedLetter)
    {
        ExcelColumnHelper.IndexToColumnLetter(columnNumber).Should().Be(expectedLetter);
    }

    [Test]
    [Arguments("", 0)]
    [Arguments("   ", 0)]
    [Arguments("A1B", 0)]
    public void ColumnLetterToIndex_WithInvalidLetter_ShouldReturnZero(string letter, int expectedIndex)
    {
        ExcelColumnHelper.ColumnLetterToIndex(letter).Should().Be(expectedIndex);
    }

    [Test]
    [Arguments(null, false)]
    [Arguments("   ", false)]
    [Arguments("abc", false)]
    [Arguments("123", true)]
    public void IsNumeric_ShouldValidateCorrectly(string? input, bool expected)
    {
        ExcelColumnHelper.IsNumeric(input).Should().Be(expected);
    }

    [Test]
    [Arguments(null, false)]
    [Arguments("   ", false)]
    [Arguments("0", false)]
    [Arguments("-1", false)]
    [Arguments("123", true)]
    [Arguments("AB", true)]
    [Arguments("A1", false)]
    public void IsValidColumnLetter_ShouldValidateCorrectly(string? input, bool expected)
    {
        ExcelColumnHelper.IsValidColumnLetter(input).Should().Be(expected);
    }

    [Test]
    [Arguments("!@#", "Fallback")]
    public void NormalizeColumnLetter_WithInvalidInput_ShouldReturnFallback(string input, string expected)
    {
        ExcelColumnHelper.NormalizeColumnLetter(input, "Fallback").Should().Be(expected);
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Testing JsonConverter edge cases")]
    public void JsonConverter_ShouldHandleEdgeCasesAndSerialization()
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

        string jsonWithInvalidNumber = "{\"CategoryColumn\": -1}";
        var deserializedInvalidNum = JsonSerializer.Deserialize<SizeMatrixMapping>(jsonWithInvalidNumber);
        deserializedInvalidNum!.CategoryColumn.Should().Be("A");

        string jsonWithEmptyString = "{\"CategoryColumn\": \"\"}";
        var deserializedEmptyStr = JsonSerializer.Deserialize<SizeMatrixMapping>(jsonWithEmptyString);
        deserializedEmptyStr!.CategoryColumn.Should().Be("");

        string serialized = JsonSerializer.Serialize(new SizeMatrixMapping { CategoryColumn = "4" });
        serialized.Should().Contain("\"CategoryColumn\":\"D\"");
    }
}
