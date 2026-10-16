using System.Collections.ObjectModel;
using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Tests.Utils;
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
        _service = new ProfileManagerService(_dialogMock.Object, new BexioOrderImport.Abstractions.Formats.FormatRegistry([new PeakmileFormatAdapter()]));
    }

    [Test]
    public void CreateProfile_WhenNameIsValid_AddsAndReturnsNewProfile()
    {
        var profiles = new ObservableCollection<MappingProfile>
        {
            ProfileTestExtensions.PeakmileProfile("Default")
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("Custom Profile", PeakmileFormatAdapter.Id));

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
            ProfileTestExtensions.PeakmileProfile("Default")
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("Default", PeakmileFormatAdapter.Id));

        var created = _service.CreateProfile(profiles);

        created.Should().BeNull();
        profiles.Should().HaveCount(1);
        _dialogMock.Verify(d => d.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void DeleteProfile_WhenConfirmed_RemovesProfile()
    {
        var defaultP = ProfileTestExtensions.PeakmileProfile("Default");
        var customP = ProfileTestExtensions.PeakmileProfile("Custom");
        var profiles = new ObservableCollection<MappingProfile> { defaultP, customP };

        _dialogMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        bool result = _service.DeleteProfile(profiles, customP);

        result.Should().BeTrue();
        profiles.Should().HaveCount(1);
        profiles.Should().NotContain(customP);
    }

    [Test]
    public void CreateProfile_WhenUserCancels_ReturnsNull()
    {
        var profiles = new ObservableCollection<MappingProfile>
        {
            ProfileTestExtensions.PeakmileProfile("Default")
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns((ProfileCreateResult?)null);

        var created = _service.CreateProfile(profiles);

        created.Should().BeNull();
        profiles.Should().HaveCount(1);
    }

    [Test]
    public void CloneProfile_WhenUserCancels_ReturnsNull()
    {
        var defaultP = ProfileTestExtensions.PeakmileProfile("Default");
        var profiles = new ObservableCollection<MappingProfile> { defaultP };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(PeakmileFormatAdapter.Id)).Returns((ProfileCreateResult?)null);

        var cloned = _service.CloneProfile(profiles, defaultP);

        cloned.Should().BeNull();
        profiles.Should().HaveCount(1);
    }

    [Test]
    public void ImportProfiles_WhenFileReadThrowsException_ShowsErrorAndReturnsFalse()
    {
        var profiles = new ObservableCollection<MappingProfile>();
        _dialogMock.Setup(d => d.ShowOpenFileDialog(It.IsAny<string>(), It.IsAny<string>())).Returns("invalid_file_path.json");

        bool result = _service.ImportProfiles(profiles, s => { });

        result.Should().BeFalse();
        _dialogMock.Verify(d => d.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void CreateProfile_ShouldUseTheChosenFormatWithDefaultSettings()
    {
        var profiles = new ObservableCollection<MappingProfile>();
        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("New", "PEAKMILE"));

        var created = _service.CreateProfile(profiles);

        created!.FormatId.Should().Be(PeakmileFormatAdapter.Id);
        created.Settings.Should().BeEquivalentTo(new PeakmileSettings());
    }

    [Test]
    public void CreateProfile_WhenFormatIsUnknown_ReturnsNull()
    {
        var profiles = new ObservableCollection<MappingProfile>();
        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("New", "future-format"));

        var created = _service.CreateProfile(profiles);

        created.Should().BeNull();
        profiles.Should().BeEmpty();
    }

    [Test]
    public void CloneProfile_ShouldKeepTheFormatAndCopyTheSettings()
    {
        var source = ProfileTestExtensions.PeakmileProfile("Default", new PeakmileSettings { SeasonCode = "HW26" });
        var profiles = new ObservableCollection<MappingProfile> { source };
        _dialogMock.Setup(d => d.ShowProfileCreateDialog(PeakmileFormatAdapter.Id)).Returns(new ProfileCreateResult("Copy", PeakmileFormatAdapter.Id));

        var cloned = _service.CloneProfile(profiles, source);

        cloned!.Name.Should().Be("Copy");
        cloned.FormatId.Should().Be(PeakmileFormatAdapter.Id);
        cloned.Peakmile().SeasonCode.Should().Be("HW26");
        cloned.Settings.Should().NotBeSameAs(source.Settings);
    }
}
