using System.Collections.ObjectModel;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Wpf.Services;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class ProfileManagerServiceTests
{
    private readonly Mock<IDialogService> _dialogMock;
    private readonly ProfileManagerService _service;

    public ProfileManagerServiceTests()
    {
        _dialogMock = new Mock<IDialogService>();
        _service = new ProfileManagerService(_dialogMock.Object);
    }

    [Test]
    public void CreateProfile_WhenNameIsValid_AddsAndReturnsNewProfile()
    {
        var profiles = new ObservableCollection<MappingProfile>
        {
            new MappingProfile { Name = "Default", Mapping = new ExcelMappingOptions() }
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(false)).Returns("Custom Profile");

        var created = _service.CreateProfile(profiles);

        created.Should().NotBeNull();
        created!.Name.Should().Be("Custom Profile");
        profiles.Should().HaveCount(2);
    }

    [Test]
    public void CreateProfile_WhenNameAlreadyExists_ShowsErrorAndReturnsNull()
    {
        var profiles = new ObservableCollection<MappingProfile>
        {
            new MappingProfile { Name = "Default", Mapping = new ExcelMappingOptions() }
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(false)).Returns("Default");

        var created = _service.CreateProfile(profiles);

        created.Should().BeNull();
        profiles.Should().HaveCount(1);
        _dialogMock.Verify(d => d.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void DeleteProfile_WhenConfirmed_RemovesProfile()
    {
        var defaultP = new MappingProfile { Name = "Default", Mapping = new ExcelMappingOptions() };
        var customP = new MappingProfile { Name = "Custom", Mapping = new ExcelMappingOptions() };
        var profiles = new ObservableCollection<MappingProfile> { defaultP, customP };

        _dialogMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        bool result = _service.DeleteProfile(profiles, customP);

        result.Should().BeTrue();
        profiles.Should().HaveCount(1);
        profiles.Should().NotContain(customP);
    }
}
