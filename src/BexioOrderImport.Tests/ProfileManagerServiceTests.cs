using System.Collections.ObjectModel;
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
        _service = new ProfileManagerService(_dialogMock.Object, new BexioOrderImport.Abstractions.Formats.FormatRegistry([new FakeFormatAdapter()]));
    }

    [Test]
    public void CreateProfile_WhenNameIsValid_AddsAndReturnsNewProfile()
    {
        var profiles = new ObservableCollection<MappingProfile>
        {
            ProfileTestExtensions.FakeProfile("Default")
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("Custom Profile", FakeFormatAdapter.Id));

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
            ProfileTestExtensions.FakeProfile("Default")
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("Default", FakeFormatAdapter.Id));

        var created = _service.CreateProfile(profiles);

        created.Should().BeNull();
        profiles.Should().HaveCount(1);
        _dialogMock.Verify(d => d.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void DeleteProfile_WhenConfirmed_RemovesProfile()
    {
        var defaultP = ProfileTestExtensions.FakeProfile("Default");
        var customP = ProfileTestExtensions.FakeProfile("Custom");
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
            ProfileTestExtensions.FakeProfile("Default")
        };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns((ProfileCreateResult?)null);

        var created = _service.CreateProfile(profiles);

        created.Should().BeNull();
        profiles.Should().HaveCount(1);
    }

    [Test]
    public void CloneProfile_WhenUserCancels_ReturnsNull()
    {
        var defaultP = ProfileTestExtensions.FakeProfile("Default");
        var profiles = new ObservableCollection<MappingProfile> { defaultP };

        _dialogMock.Setup(d => d.ShowProfileCreateDialog(FakeFormatAdapter.Id)).Returns((ProfileCreateResult?)null);

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
        _dialogMock.Setup(d => d.ShowProfileCreateDialog(null)).Returns(new ProfileCreateResult("New", "FAKE"));

        var created = _service.CreateProfile(profiles);

        created!.FormatId.Should().Be(FakeFormatAdapter.Id);
        created.Settings.Should().BeEquivalentTo(new FakeSettings());
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
        var source = ProfileTestExtensions.FakeProfile("Default", new FakeSettings { Value = "HW26" });
        var profiles = new ObservableCollection<MappingProfile> { source };
        _dialogMock.Setup(d => d.ShowProfileCreateDialog(FakeFormatAdapter.Id)).Returns(new ProfileCreateResult("Copy", FakeFormatAdapter.Id));

        var cloned = _service.CloneProfile(profiles, source);

        cloned!.Name.Should().Be("Copy");
        cloned.FormatId.Should().Be(FakeFormatAdapter.Id);
        ((FakeSettings)cloned.Settings!).Value.Should().Be("HW26");
        cloned.Settings.Should().NotBeSameAs(source.Settings);
    }
}
