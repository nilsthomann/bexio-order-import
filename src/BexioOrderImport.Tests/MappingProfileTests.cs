using BexioOrderImport.Application.Options;
using BexioOrderImport.Wpf.Models;
using FluentAssertions;
using Xunit;

namespace BexioOrderImport.Tests;

public class MappingProfileTests
{
    [Fact]
    public void NameProperty_WhenChanged_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var profile = new MappingProfile { Name = "InitialName" };
        string? propertyChangedName = null;
        profile.PropertyChanged += (sender, args) =>
        {
            propertyChangedName = args.PropertyName;
        };

        // Act
        profile.Name = "UpdatedName";

        // Assert
        profile.Name.Should().Be("UpdatedName");
        propertyChangedName.Should().Be(nameof(MappingProfile.Name));
    }

    [Fact]
    public void MappingProperty_WhenChanged_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var profile = new MappingProfile();
        string? propertyChangedName = null;
        profile.PropertyChanged += (sender, args) =>
        {
            propertyChangedName = args.PropertyName;
        };

        var newMapping = new ExcelMappingOptions { WorksheetIndex = 2 };

        // Act
        profile.Mapping = newMapping;

        // Assert
        profile.Mapping.Should().BeSameAs(newMapping);
        propertyChangedName.Should().Be(nameof(MappingProfile.Mapping));
    }
}
