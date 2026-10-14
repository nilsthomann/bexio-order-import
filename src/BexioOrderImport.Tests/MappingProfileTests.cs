using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Wpf.Models;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class MappingProfileTests
{
    [Test]
    public void NameProperty_WhenChanged_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var profile = new MappingProfile { Name = "InitialName", FormatId = PeakmileFormatAdapter.Id };
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

    [Test]
    public void SettingsProperty_WhenChanged_ShouldRaisePropertyChangedEvent()
    {
        // Arrange
        var profile = new MappingProfile { FormatId = PeakmileFormatAdapter.Id };
        string? propertyChangedName = null;
        profile.PropertyChanged += (sender, args) =>
        {
            propertyChangedName = args.PropertyName;
        };

        var newSettings = new PeakmileSettings { WorksheetIndex = 2 };

        // Act
        profile.Settings = newSettings;

        // Assert
        profile.Settings.Should().BeSameAs(newSettings);
        propertyChangedName.Should().Be(nameof(MappingProfile.Settings));
    }
}
