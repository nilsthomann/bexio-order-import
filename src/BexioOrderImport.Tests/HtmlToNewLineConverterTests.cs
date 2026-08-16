using System;
using System.Globalization;
using BexioOrderImport.Wpf.Converters;
using FluentAssertions;
using Xunit;

namespace BexioOrderImport.Tests;

public class HtmlToNewLineConverterTests
{
    private readonly HtmlToNewLineConverter _converter = new();

    [Theory]
    [InlineData("1x Size S<br />2x Size L", "1x Size S\n2x Size L")]
    [InlineData("1x Size S<br/>2x Size L", "1x Size S\n2x Size L")]
    [InlineData("1x Size S<br>2x Size L", "1x Size S\n2x Size L")]
    [InlineData("1x Size S<BR />2x Size L", "1x Size S\n2x Size L")]
    public void Convert_WithHtmlBreakTags_ShouldReplaceWithNewLine(string input, string expected)
    {
        // Act
        var result = _converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected.Replace("\n", Environment.NewLine));
    }

    [Fact]
    public void Convert_WithPlainText_ShouldReturnUnchanged()
    {
        // Arrange
        string input = "120";

        // Act
        var result = _converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("120");
    }

    [Fact]
    public void Convert_WithNullOrEmpty_ShouldReturnEmptyString()
    {
        // Act & Assert
        _converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture).Should().Be(string.Empty);
        _converter.Convert(string.Empty, typeof(string), null!, CultureInfo.InvariantCulture).Should().Be(string.Empty);
    }

    [Fact]
    public void ConvertBack_ShouldThrowNotImplementedException()
    {
        // Act
        Action act = () => _converter.ConvertBack("test", typeof(string), null!, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotImplementedException>();
    }
}
