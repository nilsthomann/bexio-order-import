using BexioOrderImport.Application.Helpers;
using FluentAssertions;
using Xunit;

namespace BexioOrderImport.Tests;

public class IdNormalizationHelperTests
{
    [Theory]
    [InlineData("2", 2)]
    [InlineData("#2", 2)]
    [InlineData("00002", 2)]
    [InlineData("#00002", 2)]
    [InlineData("  #00123 ", 123)]
    [InlineData("99999", 99999)]
    public void NormalizeCustomerId_WithValidInputs_ShouldReturnNormalizedInteger(string input, int expected)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeCustomerId(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("BL-002")]
    [InlineData("AU-00002")]
    [InlineData("AU-2")]
    [InlineData("2A")]
    [InlineData("0")]
    [InlineData("0000")]
    [InlineData("#0000")]
    [InlineData("-1")]
    [InlineData("XYZ")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeCustomerId_WithInvalidInputs_ShouldReturnNull(string? input)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeCustomerId(input);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("#2", 2)]
    [InlineData("00002", 2)]
    [InlineData("#00002", 2)]
    [InlineData("AU-2", 2)]
    [InlineData("AU-00002", 2)]
    [InlineData("au-00002", 2)]
    [InlineData("#AU-00002", 2)]
    [InlineData("AU-#00002", 2)]
    [InlineData("  AU-00456 ", 456)]
    public void NormalizeOrderId_WithValidInputs_ShouldReturnNormalizedInteger(string input, int expected)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeOrderId(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("BL-002")]
    [InlineData("BL-2")]
    [InlineData("XY-00002")]
    [InlineData("2A")]
    [InlineData("AU-00002X")]
    [InlineData("0")]
    [InlineData("AU-0000")]
    [InlineData("-5")]
    [InlineData("ABC")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeOrderId_WithInvalidInputs_ShouldReturnNull(string? input)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeOrderId(input);

        // Assert
        result.Should().BeNull();
    }
}
