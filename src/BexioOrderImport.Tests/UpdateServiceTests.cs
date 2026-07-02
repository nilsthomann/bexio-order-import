using System;
using Xunit;
using FluentAssertions;
using BexioOrderImport.Wpf.Services;

namespace BexioOrderImport.Tests;

public class UpdateServiceTests
{
    private readonly UpdateService _updateService;

    public UpdateServiceTests()
    {
        _updateService = new UpdateService();
    }

    [Theory]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v0.9.0", "1.0.0", false)]
    [InlineData("v1.0.0", null, false)]
    [InlineData("invalid-version", "1.0.0", false)]
    public void IsNewerVersion_ShouldCorrectlyCompareVersions(string rawTag, string? currentVersionStr, bool expectedResult)
    {
        // Arrange
        Version? currentVersion = currentVersionStr != null ? Version.Parse(currentVersionStr) : null;

        // Act
        bool result = _updateService.IsNewerVersion(rawTag, currentVersion);

        // Assert
        result.Should().Be(expectedResult);
    }
}
