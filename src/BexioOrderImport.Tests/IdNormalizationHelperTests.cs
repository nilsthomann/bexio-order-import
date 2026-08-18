using BexioOrderImport.Application.Helpers;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class IdNormalizationHelperTests
{
    [Test]
    [Arguments("2", 2)]
    [Arguments("#2", 2)]
    [Arguments("00002", 2)]
    [Arguments("#00002", 2)]
    [Arguments("  #00123 ", 123)]
    [Arguments("99999", 99999)]
    public void NormalizeCustomerId_WithValidInputs_ShouldReturnNormalizedInteger(string input, int expected)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeCustomerId(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    [Arguments("BL-002")]
    [Arguments("AU-00002")]
    [Arguments("AU-2")]
    [Arguments("2A")]
    [Arguments("0")]
    [Arguments("0000")]
    [Arguments("#0000")]
    [Arguments("-1")]
    [Arguments("XYZ")]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void NormalizeCustomerId_WithInvalidInputs_ShouldReturnNull(string? input)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeCustomerId(input);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    [Arguments("2", 2)]
    [Arguments("#2", 2)]
    [Arguments("00002", 2)]
    [Arguments("#00002", 2)]
    [Arguments("AU-2", 2)]
    [Arguments("AU-00002", 2)]
    [Arguments("au-00002", 2)]
    [Arguments("#AU-00002", 2)]
    [Arguments("AU-#00002", 2)]
    [Arguments("  AU-00456 ", 456)]
    public void NormalizeOrderId_WithValidInputs_ShouldReturnNormalizedInteger(string input, int expected)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeOrderId(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    [Arguments("BL-002")]
    [Arguments("BL-2")]
    [Arguments("XY-00002")]
    [Arguments("2A")]
    [Arguments("AU-00002X")]
    [Arguments("0")]
    [Arguments("AU-0000")]
    [Arguments("-5")]
    [Arguments("ABC")]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void NormalizeOrderId_WithInvalidInputs_ShouldReturnNull(string? input)
    {
        // Act
        int? result = IdNormalizationHelper.NormalizeOrderId(input);

        // Assert
        result.Should().BeNull();
    }
}
