using System.Text.Json;
using BexioOrderImport.Application.Helpers;
using BexioOrderImport.Application.Options;
using FluentAssertions;
using Xunit;

namespace BexioOrderImport.Tests;

public class ExcelColumnHelperTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(2, "B")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(28, "AB")]
    [InlineData(52, "AZ")]
    [InlineData(53, "BA")]
    [InlineData(702, "ZZ")]
    [InlineData(703, "AAA")]
    public void IndexToColumnLetter_ShouldConvertCorrectly(int columnNumber, string expectedLetter)
    {
        ExcelColumnHelper.IndexToColumnLetter(columnNumber).Should().Be(expectedLetter);
    }

    [Theory]
    [InlineData("A", 1)]
    [InlineData("b", 2)]
    [InlineData("Z", 26)]
    [InlineData("AA", 27)]
    [InlineData("ab", 28)]
    [InlineData("AZ", 52)]
    [InlineData("BA", 53)]
    [InlineData("ZZ", 702)]
    [InlineData("AAA", 703)]
    public void ColumnLetterToIndex_ShouldConvertCorrectly(string letter, int expectedIndex)
    {
        ExcelColumnHelper.ColumnLetterToIndex(letter).Should().Be(expectedIndex);
    }

    [Theory]
    [InlineData("1", "A")]
    [InlineData("4", "D")]
    [InlineData("18", "R")]
    [InlineData("a", "A")]
    [InlineData("  b  ", "B")]
    [InlineData("AA", "AA")]
    [InlineData("", "Fallback")]
    public void NormalizeColumnLetter_ShouldNormalizeNumericAndStringValues(string input, string expected)
    {
        ExcelColumnHelper.NormalizeColumnLetter(input, "Fallback").Should().Be(expected);
    }

    [Fact]
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
