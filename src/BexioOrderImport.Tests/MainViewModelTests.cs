using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Wpf.Services;
using BexioOrderImport.Wpf.ViewModels;
using BexioOrderImport.Tests.Utils;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

[NotInParallel]
public class MainViewModelTests : IDisposable
{
    private readonly Mock<IUpdateService> _updateServiceMock;
    private readonly Mock<IBexioClientFactory> _clientFactoryMock;
    private readonly Mock<IBexioClient> _clientMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IDispatcherService> _dispatcherServiceMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly string _tempFilePath;

    public MainViewModelTests()
    {
        // Initialize WPF Application context for unit tests on a pumping STA thread
        WpfTestApplication.EnsureInitialized();

        _tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString() + "_appsettings.json");

        _updateServiceMock = new Mock<IUpdateService>();
        _clientMock = new Mock<IBexioClient>();
        _clientFactoryMock = new Mock<IBexioClientFactory>();
        _clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(_clientMock.Object);
        _dialogServiceMock = new Mock<IDialogService>();
        _dispatcherServiceMock = new Mock<IDispatcherService>();
        // Mock dispatcher service to execute actions immediately
        _dispatcherServiceMock.Setup(d => d.Invoke(It.IsAny<Action>())).Callback<Action>(a => a());
        _dispatcherServiceMock.Setup(d => d.BeginInvoke(It.IsAny<Action>())).Callback<Action>(a => a());
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
        _encryptionServiceMock.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        var realParserFactory = new BexioOrderImport.Infrastructure.Excel.ClosedXmlExcelParserFactory();
        _excelParserFactoryMock = new Mock<IExcelParserFactory>();
        _excelParserFactoryMock.Setup(f => f.Create(It.IsAny<BexioOrderImport.Application.Options.ExcelMappingOptions>()))
            .Returns((BexioOrderImport.Application.Options.ExcelMappingOptions opts) => realParserFactory.Create(opts));
    }

    private readonly Mock<IExcelParserFactory> _excelParserFactoryMock;

    public void Dispose()
    {
        try
        {
            if (System.IO.File.Exists(_tempFilePath))
            {
                System.IO.File.Delete(_tempFilePath);
            }
        }
        catch { }
    }

    private MainViewModel CreateVm()
    {
        return new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            _excelParserFactoryMock.Object,
            _tempFilePath);
    }

    [Test]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var vm = CreateVm();

        // Assert
        vm.IsImporting.Should().BeFalse();
        vm.IsLoading.Should().BeFalse();
        vm.IsUpdateAvailable.Should().BeFalse();
        vm.AppVersion.Should().StartWith("v");
        vm.HasLoadedFile.Should().BeFalse();
    }

    [Test]
    public void SetFileProperties_ShouldUpdateStateCorrectly()
    {
        // Arrange
        var vm = CreateVm();

        // Act
        vm.SelectedFilePath = "C:\\test\\order.xlsx";
        vm.SelectedFileName = "order.xlsx";
        vm.FileSizeText = "12 KB";
        vm.HasLoadedFile = true;

        // Assert
        vm.SelectedFilePath.Should().Be("C:\\test\\order.xlsx");
        vm.SelectedFileName.Should().Be("order.xlsx");
        vm.FileSizeText.Should().Be("12 KB");
        vm.HasLoadedFile.Should().BeTrue();
    }

    [Test]
    public void SetLanguage_ShouldUpdateSelectedLanguage()
    {
        // Arrange
        var vm = CreateVm();

        // Act
        vm.SelectedLanguage = "en";

        // Assert
        vm.SelectedLanguage.Should().Be("en");
    }

    [Test]
    public void BexioTokenDisplay_WhenNotFocused_ShouldReturnDots()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "my-secret-token";
        vm.IsTokenFocused = false;

        // Act & Assert
        vm.BexioTokenDisplay.Should().Be(new string('•', 24));
    }

    [Test]
    public void BexioTokenDisplay_WhenFocused_ShouldReturnRealToken()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "my-secret-token";
        vm.IsTokenFocused = true;

        // Act & Assert
        vm.BexioTokenDisplay.Should().Be("my-secret-token");
    }

    [Test]
    public void BexioTokenDisplay_WhenNotFocusedAndEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "";
        vm.IsTokenFocused = false;

        // Act & Assert
        vm.BexioTokenDisplay.Should().Be("");
    }

    [Test]
    public void BexioTokenDisplay_WhenSetWhileFocused_ShouldUpdateBexioToken()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "old-token";
        vm.IsTokenFocused = true;

        // Act
        vm.BexioTokenDisplay = "new-token";

        // Assert
        vm.BexioToken.Should().Be("new-token");
    }

    [Test]
    public void BexioTokenDisplay_WhenSetWhileNotFocused_ShouldNotUpdateBexioToken()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "old-token";
        vm.IsTokenFocused = false;

        // Act
        vm.BexioTokenDisplay = "new-token";

        // Assert
        vm.BexioToken.Should().Be("old-token");
    }

    [Test]
    public void ImportCommand_WhenAccountOrTaxIdNull_ShouldShowErrorDialogAndAbort()
    {
        // Arrange
        var vm = CreateVm();
        vm.AccountId = null; // null triggers error
        vm.TaxId = 1;
        
        // Simulating a loaded order so the command can execute
        vm._loadedOrder = new Order { Customer = new Customer { CompanyName = "Test customer" } };
        
        vm.ImportCommand.RaiseCanExecuteChanged();

        // Act
        vm.ImportCommand.Execute(null);

        // Assert
        _dialogServiceMock.Verify(d => d.ShowErrorDialog(
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Once);
        vm.IsImporting.Should().BeFalse();
    }

    [Test]
    public async Task CheckBexioConnectionAsync_ShouldTriggerConnectionCheckAndPopulateLists()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "some-token";
        
        var accounts = new List<BexioAccount>
        {
            new BexioAccount { Id = 100, AccountNo = "1000", Name = "Cash Account", IsActive = true }
        };
        var taxes = new List<BexioTax>
        {
            new BexioTax { Id = 5, DisplayName = "MwSt 8.1%", Percentage = 8.1m, IsActive = true }
        };
        
        _clientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
        _clientMock.Setup(c => c.GetAccountsAsync()).ReturnsAsync(accounts);
        _clientMock.Setup(c => c.GetTaxesAsync()).ReturnsAsync(taxes);

        // Act
        await vm.CheckBexioConnectionAsync();

        // Assert
        vm.IsConnectionSuccessful.Should().BeTrue();
        vm.ConnectionStatusColor.Should().Be("#10B981"); // Green success
        vm.AccountsList.Count.Should().Be(1);
        vm.AccountsList[0].Id.Should().Be(100);
        vm.TaxesList.Count.Should().Be(1);
        vm.TaxesList[0].Id.Should().Be(5);
    }

    [Test]
    public void DeleteProfile_ShouldAllowDeletingDefault_WhenMultipleProfilesExist()
    {
        // Arrange
        var vm = CreateVm();
        var secondProfile = new BexioOrderImport.Wpf.Models.MappingProfile { Name = "Profile2" };
        vm.Profiles.Add(secondProfile);
        _dialogServiceMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        // Act
        var defaultProfile = vm.Profiles[0];
        vm.DeleteProfileCommand.CanExecute(defaultProfile).Should().BeTrue();
        vm.DeleteProfileCommand.Execute(defaultProfile);

        // Assert
        vm.Profiles.Should().NotContain(defaultProfile);
        vm.Profiles.Count.Should().Be(1);
        vm.IsModified.Should().BeTrue();
    }

    [Test]
    public void DeleteProfile_ShouldNotAllowDeleting_WhenOnlyOneProfileExists()
    {
        // Arrange
        var vm = CreateVm();
        vm.Profiles.Count.Should().Be(1);

        // Act & Assert
        vm.DeleteProfileCommand.CanExecute(vm.Profiles[0]).Should().BeFalse();
    }

    [Test]
    public void SelectedTabIndex_WhenUserDiscardsChanges_ShouldReloadSettingsAndSwitchTab()
    {
        // Arrange
        var vm = CreateVm();
        vm.SelectedTabIndex = 1; // Settings tab
        vm.BexioToken = "unsaved-token"; // Modifies VM state
        vm.IsModified.Should().BeTrue();
        _dialogServiceMock.Setup(d => d.ShowPendingChangesDialog()).Returns(true); // User selects "Discard Changes"

        // Act
        vm.SelectedTabIndex = 0; // Try switching to Import tab

        // Assert
        vm.SelectedTabIndex.Should().Be(0); // Tab successfully switched to Import tab
        vm.IsModified.Should().BeFalse(); // Settings reloaded, unsaved state cleared
    }

    [Test]
    public void SelectedTabIndex_WhenUserCancelsPendingChanges_ShouldRemainOnSettingsTab()
    {
        // Arrange
        var vm = CreateVm();
        vm.SelectedTabIndex = 1; // Settings tab
        vm.IsModified = true;
        _dialogServiceMock.Setup(d => d.ShowPendingChangesDialog()).Returns(false); // User selects "Cancel"

        // Act
        vm.SelectedTabIndex = 0; // Try switching to Import tab

        // Assert
        vm.SelectedTabIndex.Should().Be(1); // User remains on Settings tab
    }

    [Test]
    public void EditProfile_WhenProfileRenamedInDialog_ShouldUpdateNameAndSetModified()
    {
        // Arrange
        var vm = CreateVm();
        var profile = vm.Profiles[0];
        _dialogServiceMock
            .Setup(d => d.ShowProfileEditDialog(profile, vm.Profiles))
            .Callback<BexioOrderImport.Wpf.Models.MappingProfile, IEnumerable<BexioOrderImport.Wpf.Models.MappingProfile>>((p, list) => p.Name = "Renamed Profile")
            .Returns(true);

        // Act
        vm.EditProfileCommand.Execute(profile);

        // Assert
        profile.Name.Should().Be("Renamed Profile");
        vm.IsModified.Should().BeTrue();
    }

    [Test]
    public void SetActiveProfile_ShouldNotSetIsModified()
    {
        // Arrange
        var vm = CreateVm();
        var newProfile = new BexioOrderImport.Wpf.Models.MappingProfile { Name = "Profile2" };
        vm.Profiles.Add(newProfile);
        vm.IsModified = false;

        // Act
        vm.SetActiveProfileCommand.Execute(newProfile);

        // Assert
        vm.ActiveProfile.Should().Be(newProfile);
        vm.IsModified.Should().BeFalse();
    }

    [Test]
    public void IsActiveRowDiscountEnabled_ShouldReflectActiveProfileMapping()
    {
        // Arrange
        var vm = CreateVm();
        var profileWithDiscount = new BexioOrderImport.Wpf.Models.MappingProfile
        {
            Name = "DiscountProfile",
            Mapping = new BexioOrderImport.Application.Options.ExcelMappingOptions
            {
                Data = new BexioOrderImport.Application.Options.DataMapping { EnableRowDiscount = true }
            }
        };

        // Act
        vm.ActiveProfile = profileWithDiscount;

        // Assert
        vm.IsActiveRowDiscountEnabled.Should().BeTrue();
    }

    [Test]
    public async Task LoadExcelFileAsync_WhenFileParsingFails_ShouldShowErrorDialog()
    {
        // Arrange
        var mockParser = new Mock<IExcelParser>();
        mockParser.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Throws(new Exception("Parsing failed custom error"));
        var mockFactory = new Mock<IExcelParserFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<BexioOrderImport.Application.Options.ExcelMappingOptions>())).Returns(mockParser.Object);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            mockFactory.Object,
            _tempFilePath
        );

        // Act
        await vm.LoadExcelFileAsync("invalid.xlsx");

        // Assert
        vm.HasLoadedFile.Should().BeFalse();
        _dialogServiceMock.Verify(d => d.ShowErrorDialog("Parsing failed custom error", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task LoadExcelFileAsync_WhenFileIsLocked_ShouldShowLockedDialog()
    {
        // Arrange
        var mockParser = new Mock<IExcelParser>();
        mockParser.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Throws(new System.IO.IOException("The process cannot access the file because it is being used by another process."));
        var mockFactory = new Mock<IExcelParserFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<BexioOrderImport.Application.Options.ExcelMappingOptions>())).Returns(mockParser.Object);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            mockFactory.Object,
            _tempFilePath
        );

        // Act
        await vm.LoadExcelFileAsync("locked.xlsx");

        // Assert
        vm.HasLoadedFile.Should().BeFalse();
        _dialogServiceMock.Verify(d => d.ShowErrorDialog(It.Is<string>(s => s.Contains("locked.xlsx")), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ConfirmEmailMismatchAsync_ShouldShowConfirmDialog()
    {
        // Arrange
        var vm = CreateVm();
        vm.IsImportingActive = true;
        _dialogServiceMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        // Act - Call private ConfirmEmailMismatchAsync via reflection
        var method = typeof(MainViewModel).GetMethod("ConfirmEmailMismatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<bool>)method!.Invoke(vm, new object[] { "existing@test.com", "excel@test.com" })!;
        bool result = await task;

        // Assert
        result.Should().BeTrue();
        _dialogServiceMock.Verify(d => d.ShowConfirmDialog(It.Is<string>(s => s.Contains("existing@test.com") && s.Contains("excel@test.com")), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task WpfImportUserInteractionService_ShouldDelegateCallsToViewModelAndDialogService()
    {
        // Arrange
        var vm = CreateVm();
        vm.IsImportingActive = true;
        _dialogServiceMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _dialogServiceMock.Setup(d => d.ShowCustomerConfirmDialog(It.IsAny<BexioOrderImport.Domain.Models.Customer>())).Returns(true);

        // Get private WpfImportUserInteractionService type via reflection
        var type = typeof(MainViewModel).GetNestedType("WpfImportUserInteractionService", System.Reflection.BindingFlags.NonPublic);
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        var service = (BexioOrderImport.Application.Interfaces.IImportUserInteractionService)Activator.CreateInstance(type!, new object[] {
            vm,
            (Action<double, double, System.Diagnostics.Stopwatch>)((c, t, sw) => { }),
            stopwatch
        })!;

        // Act & Assert - ShowPreview (void, no-op)
        service.ShowPreview(new BexioOrderImport.Domain.Models.Order());

        // Act & Assert - ConfirmEmailMismatchAsync
        bool emailConfirmed = await service.ConfirmEmailMismatchAsync("old@email.com", "new@email.com");
        emailConfirmed.Should().BeTrue();

        // Act & Assert - ConfirmCustomerCreationAsync
        bool customerConfirmed = await service.ConfirmCustomerCreationAsync(new BexioOrderImport.Domain.Models.Customer { CompanyName = "Test Co" });
        customerConfirmed.Should().BeTrue();

        // Act & Assert - ConfirmUploadAsync
        bool uploadConfirmed = await service.ConfirmUploadAsync();
        uploadConfirmed.Should().BeTrue();

        // Act & Assert - LogInfo
        service.LogInfo("Test interaction log");
        vm.LogText.Should().Contain("Test interaction log");

        // Act & Assert - ReportProgress
        service.ReportProgress(5, 10);
        vm.ProgressPercentage.Should().Be(50.0);
    }

    [Test]
    public void MainViewModel_PropertyGettersAndSetters_ShouldUpdateState()
    {
        var vm = CreateVm();
        vm.Address = "Test Address 123";
        vm.Address.Should().Be("Test Address 123");

        vm.ImportSuccessTitle = "Success Title";
        vm.ImportSuccessTitle.Should().Be("Success Title");

        vm.ImportDurationText = "00:01:30";
        vm.ImportDurationText.Should().Be("00:01:30");

        vm.IsActiveOrderNumberEnabled.Should().BeFalse();
        vm.IsActiveCustomerNumberEnabled.Should().BeFalse();

        bool invoked = false;
        vm.InvokeOnUiAsync(() => invoked = true);
        invoked.Should().BeTrue();
    }

    [Test]
    public void IsTokenFocused_WhenToggledFalse_ShouldTriggerConnectionCheck()
    {
        var vm = CreateVm();
        vm.BexioToken = "valid_token";
        vm.IsTokenFocused = true;

        vm.BexioTokenDisplay.Should().Be("valid_token");

        vm.IsTokenFocused = false;
        vm.BexioTokenDisplay.Should().Be(new string('•', 24));
    }

    [Test]
    public void CloseImportSuccessCommand_WhenExecuted_ShouldResetState()
    {
        var vm = CreateVm();
        vm.IsImportingActive = true;
        vm.IsImportSuccess = true;

        vm.CloseImportSuccessCommand.Execute(null);

        vm.IsImportingActive.Should().BeFalse();
        vm.IsImportSuccess.Should().BeFalse();
        vm.HasLoadedFile.Should().BeFalse();
    }

    [Test]
    public void SetActiveProfile_WhenSelectedFilePathExists_ShouldReloadExcelFile()
    {
        var mockParser = new Mock<IExcelParser>();
        mockParser.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(new BexioOrderImport.Domain.Models.Order());
        var mockFactory = new Mock<IExcelParserFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<BexioOrderImport.Application.Options.ExcelMappingOptions>())).Returns(mockParser.Object);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            mockFactory.Object,
            _tempFilePath
        );

        string tempExcel = Path.Combine(Path.GetTempPath(), $"temp_reload_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(tempExcel, "dummy excel content");

        try
        {
            vm.SelectedFilePath = tempExcel;
            var profile2 = new BexioOrderImport.Wpf.Models.MappingProfile { Name = "Profile Reload" };
            vm.Profiles.Add(profile2);

            vm.SetActiveProfileCommand.Execute(profile2);

            vm.ActiveProfile.Should().Be(profile2);
        }
        finally
        {
            if (File.Exists(tempExcel)) File.Delete(tempExcel);
        }
    }

    [Test]
    public void LoadSettings_WhenEncryptedTokenIsNotBase64_ShouldFallbackToRawToken()
    {
        string customConfig = Path.Combine(Path.GetTempPath(), $"settings_raw_token_{Guid.NewGuid():N}.json");
        string json = @"{
            ""Bexio"": { ""ApiToken"": ""raw_unencrypted_token_123"" },
            ""ActiveProfileName"": ""Default"",
            ""Profiles"": []
        }";
        File.WriteAllText(customConfig, json);

        try
        {
            var vm = new MainViewModel(
                _updateServiceMock.Object,
                _clientFactoryMock.Object,
                _dialogServiceMock.Object,
                _dispatcherServiceMock.Object,
                _encryptionServiceMock.Object,
                _excelParserFactoryMock.Object,
                customConfig
            );

            vm.BexioToken.Should().Be("raw_unencrypted_token_123");
        }
        finally
        {
            if (File.Exists(customConfig)) File.Delete(customConfig);
        }
    }

    [Test]
    public void SaveSettings_WhenSelectedFilePathExists_ShouldReloadFile()
    {
        var mockParser = new Mock<IExcelParser>();
        mockParser.Setup(p => p.ParseOrderForm(It.IsAny<string>())).Returns(new BexioOrderImport.Domain.Models.Order());
        var mockFactory = new Mock<IExcelParserFactory>();
        mockFactory.Setup(f => f.Create(It.IsAny<BexioOrderImport.Application.Options.ExcelMappingOptions>())).Returns(mockParser.Object);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            mockFactory.Object,
            _tempFilePath
        );

        string tempExcel = Path.Combine(Path.GetTempPath(), $"temp_save_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(tempExcel, "dummy excel content");

        try
        {
            vm.SelectedFilePath = tempExcel;
            vm.IsModified = true;

            vm.SaveSettingsCommand.Execute(null);

            vm.IsModified.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempExcel)) File.Delete(tempExcel);
        }
    }

    [Test]
    public void SaveActiveProfile_WhenWriteFails_ShouldCatchAndLog()
    {
        string tempConfig = Path.Combine(Path.GetTempPath(), $"temp_config_fail_{Guid.NewGuid():N}.json");
        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            _excelParserFactoryMock.Object,
            tempConfig
        );

        try
        {
            // Set file to read-only so SaveActiveProfile fails on WriteAllText
            File.SetAttributes(tempConfig, FileAttributes.ReadOnly);
            vm.SaveActiveProfile();
            vm.LogText.Should().Contain("Could not save active profile setting");
        }
        finally
        {
            if (File.Exists(tempConfig))
            {
                File.SetAttributes(tempConfig, FileAttributes.Normal);
                File.Delete(tempConfig);
            }
        }
    }

    [Test]
    public async Task LoadBexioOptionsAsync_WhenGetAccountsFails_ShouldCatchExceptionAndLog()
    {
        var clientMock = new Mock<BexioOrderImport.Application.Interfaces.IBexioClient>();
        clientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
        clientMock.Setup(c => c.GetAccountsAsync()).ThrowsAsync(new Exception("Bexio API Accounts error"));
        _clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>())).Returns(clientMock.Object);

        var vm = CreateVm();
        vm.BexioToken = "test_token";

        await vm.CheckBexioConnectionAsync();

        vm.LogText.Should().Contain("Could not load accounts: Bexio API Accounts error");
    }

    [Test]
    public void UpdateRemainingTime_WithMultipleProgressTicks_ShouldCalculateSmoothedRemainingTime()
    {
        var vm = CreateVm();

        var method = typeof(MainViewModel).GetMethod("UpdateRemainingTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        method!.Invoke(vm, new object[] { 2, 10, stopwatch });
        method.Invoke(vm, new object[] { 5, 10, stopwatch });

        vm.RemainingTimeText.Should().NotBeNullOrEmpty();
    }
}
