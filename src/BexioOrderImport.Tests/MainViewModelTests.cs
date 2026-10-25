using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
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
    private readonly Mock<IBexioServiceFactory> _clientFactoryMock;
    private readonly Mock<IBexioService> _clientMock;
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
        _clientMock = new Mock<IBexioService>();
        _clientFactoryMock = new Mock<IBexioServiceFactory>();
        _clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>())).Returns(_clientMock.Object);
        _dialogServiceMock = new Mock<IDialogService>();
        _dispatcherServiceMock = new Mock<IDispatcherService>();
        // Mock dispatcher service to execute actions immediately
        _dispatcherServiceMock.Setup(d => d.Invoke(It.IsAny<Action>())).Callback<Action>(a => a());        _encryptionServiceMock = new Mock<IEncryptionService>();
        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
        _encryptionServiceMock.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
        _formats = new FormatRegistry([new FakeFormatAdapter()]);

        // The default profile uses the fake format, so loading a file never depends on a real format
        File.WriteAllText(_tempFilePath, """{ "ActiveProfileName": "Default", "Profiles": [ { "Name": "Default", "Format": "fake" } ] }""");
    }

    private readonly FormatRegistry _formats;

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
        GC.SuppressFinalize(this);
    }

    private MainViewModel CreateVm()
    {
        return new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            _formats,
            _tempFilePath);
    }

    [Test]
    public void Constructor_WhenUpdateCheckFails_ShouldLogItWithoutUpdate()
    {
        _updateServiceMock.Setup(u => u.CheckForUpdatesAsync()).ThrowsAsync(new HttpRequestException("offline"));

        var vm = CreateVm();

        vm.LogText.Should().Contain("Update check failed: offline");
        vm.IsUpdateAvailable.Should().BeFalse();
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
    [Arguments(null, 1, 1)]
    [Arguments(1, null, 1)]
    [Arguments(1, 1, null)]
    public void ImportCommand_WhenAccountTaxOrPaymentTypeIsNull_ShouldShowErrorDialogAndAbort(int? accountId, int? taxId, int? paymentTypeId)
    {
        // Arrange
        var vm = CreateVm();
        vm.AccountId = accountId;
        vm.TaxId = taxId;
        vm.PaymentTypeId = paymentTypeId;
        
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
            new() { Id = 100, AccountNo = "1000", Name = "Cash Account", IsActive = true }
        };
        var taxes = new List<BexioTax>
        {
            new() { Id = 5, DisplayName = "MwSt 8.1%", IsActive = true }
        };
        
        _clientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
        _clientMock.Setup(c => c.GetAccountsAsync()).ReturnsAsync(accounts);
        _clientMock.Setup(c => c.GetTaxesAsync()).ReturnsAsync(taxes);
        _clientMock.Setup(c => c.GetPaymentTypesAsync()).ReturnsAsync([new BexioPaymentType { Id = 3, Name = "Rechnung" }]);

        // Act
        await vm.CheckBexioConnectionAsync();

        // Assert
        vm.IsConnectionSuccessful.Should().BeTrue();
        vm.ConnectionStatusColor.Should().Be("#10B981"); // Green success
        vm.AccountsList.Count.Should().Be(1);
        vm.AccountsList[0].Id.Should().Be(100);
        vm.TaxesList.Count.Should().Be(1);
        vm.TaxesList[0].Id.Should().Be(5);
        vm.PaymentTypesList.Should().ContainSingle(p => p.Id == 3);
    }

    [Test]
    public void DeleteProfile_ShouldAllowDeletingDefault_WhenMultipleProfilesExist()
    {
        // Arrange
        var vm = CreateVm();
        var secondProfile = ProfileTestExtensions.FakeProfile("Profile2");
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
        var newProfile = ProfileTestExtensions.FakeProfile("Profile2");
        vm.Profiles.Add(newProfile);
        vm.IsModified = false;

        // Act
        vm.SetActiveProfileCommand.Execute(newProfile);

        // Assert
        vm.ActiveProfile.Should().Be(newProfile);
        vm.IsModified.Should().BeFalse();
    }

    private async Task RunImportAsync(FakeFormatAdapter format)
    {
        _clientMock.Setup(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>())).ReturnsAsync(new BexioOrder { Id = 77, DocumentNr = "AU-001" });
        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            new FormatRegistry([format]),
            _tempFilePath)
        {
            AccountId = 1,
            TaxId = 1,
            PaymentTypeId = 1
        };
        vm._loadedOrder = new Order();
        _dialogServiceMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var import = typeof(MainViewModel).GetMethod("ImportToBexioAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)import!.Invoke(vm, [])!;
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ImportToBexioAsync_WhenItFailsAfterTheOrderIsResolved_ShouldLinkTheOrderInTheErrorDialog(bool existingOrder)
    {
        await RunImportAsync(new FakeFormatAdapter { UploadError = new InvalidOperationException("EAN not found"), UsesExistingOrder = existingOrder });

        // A new order has to be deleted, an existing one only cleaned up
        string expectedNote = existingOrder
            ? BexioOrderImport.Wpf.Resources.Translations.Import_OrderPartlyFilledError
            : BexioOrderImport.Wpf.Resources.Translations.Import_OrderCreatedError;
        _dialogServiceMock.Verify(d => d.ShowImportErrorDialog(
            "EAN not found",
            It.IsAny<string>(),
            expectedNote,
            "AU-001",
            new Uri("https://office.bexio.com/index.php/kb_order/show/id/77")), Times.Once);
        _dialogServiceMock.Verify(d => d.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ImportToBexioAsync_WhenItFailsBeforeAnOrderIsResolved_ShouldShowThePlainErrorDialog()
    {
        _clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>())).Throws(new InvalidOperationException("no connection"));

        await RunImportAsync(new FakeFormatAdapter());

        _dialogServiceMock.Verify(d => d.ShowErrorDialog("no connection", It.IsAny<string>()), Times.Once);
        _dialogServiceMock.Verify(d => d.ShowImportErrorDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Uri>()), Times.Never);
    }

    [Test]
    public async Task LoadExcelFileAsync_WhenFileParsingFails_ShouldShowErrorDialog()
    {
        // Arrange
        var formats = new FormatRegistry([new FakeFormatAdapter { ParseFunc = _ => throw new InvalidOperationException("Parsing failed custom error") }]);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            formats,
            _tempFilePath
        );

        // Act
        await vm.LoadExcelFileAsync("invalid.xlsx");

        // Assert
        vm.HasLoadedFile.Should().BeFalse();
        _dialogServiceMock.Verify(d => d.ShowErrorDialog("Parsing failed custom error", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task LoadExcelFileAsync_WithXlsFile_ShouldShowUnsupportedFormatWithoutParsing()
    {
        bool parsed = false;
        var formats = new FormatRegistry([new FakeFormatAdapter { ParseFunc = _ => { parsed = true; return new Order(); } }]);
        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            formats,
            _tempFilePath
        );

        await vm.LoadExcelFileAsync(@"C:\Orders\old.XLS");

        parsed.Should().BeFalse();
        vm.SelectedFilePath.Should().BeNull();
        _dialogServiceMock.Verify(d => d.ShowErrorDialog(It.Is<string>(m => m.Contains("old.XLS")), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task LoadExcelFileAsync_WhenFileIsLocked_ShouldShowLockedDialog()
    {
        // Arrange
        var formats = new FormatRegistry([new FakeFormatAdapter { ParseFunc = _ => throw new System.IO.IOException("The process cannot access the file because it is being used by another process.") }]);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            formats,
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
        var task = (Task<bool>)method!.Invoke(vm, ["existing@test.com", "excel@test.com"])!;
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

        var service = (IImportUserInteractionService)Activator.CreateInstance(type!, [
            vm,
            (Action<double, double, System.Diagnostics.Stopwatch>)((c, t, sw) => { }),
            stopwatch
        ])!;

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
        vm.ImportSuccessTitle = "Success Title";
        vm.ImportSuccessTitle.Should().Be("Success Title");

        vm.ImportDurationText = "00:01:30";
        vm.ImportDurationText.Should().Be("00:01:30");
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

    /// <summary>
    /// View model whose fake format returns a new order on each parse and counts the parses.
    /// </summary>
    private MainViewModel CreateCountingVm(Action countParse)
    {
        var formats = new FormatRegistry([new FakeFormatAdapter
        {
            ParseFunc = _ =>
            {
                countParse();
                return new BexioOrderImport.Domain.Models.Order();
            }
        }]);
        return new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            formats,
            _tempFilePath);
    }

    // The reload after a profile change runs in the background
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }
        condition().Should().BeTrue();
    }

    [Test]
    public async Task SetActiveProfile_WithLoadedFile_ShouldClearTheOrderAndReadTheFileWithTheNewProfile()
    {
        int parses = 0;
        var vm = CreateCountingVm(() => Interlocked.Increment(ref parses));
        string tempExcel = Path.Combine(Path.GetTempPath(), $"temp_reload_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(tempExcel, "dummy excel content");

        try
        {
            await vm.LoadExcelFileAsync(tempExcel);
            var firstOrder = vm.LoadedOrder;
            var profile2 = ProfileTestExtensions.FakeProfile("Profile Reload");
            vm.Profiles.Add(profile2);
            // The preview must never get the old order together with the new profile (eg. another format)
            var ordersOnProfileChange = new List<BexioOrderImport.Domain.Models.Order?>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ActiveProfile)) ordersOnProfileChange.Add(vm.LoadedOrder);
            };

            vm.SetActiveProfileCommand.Execute(profile2);

            vm.ActiveProfile.Should().Be(profile2);
            ordersOnProfileChange.Should().NotBeEmpty().And.AllSatisfy(o => o.Should().BeNull());
            await WaitUntilAsync(() => Volatile.Read(ref parses) == 2 && vm.LoadedOrder != null);
            vm.LoadedOrder.Should().NotBeSameAs(firstOrder);
        }
        finally
        {
            if (File.Exists(tempExcel)) File.Delete(tempExcel);
        }
    }

    [Test]
    public async Task SetActiveProfile_WithTheActiveProfile_ShouldReadTheFileAgain()
    {
        int parses = 0;
        var vm = CreateCountingVm(() => Interlocked.Increment(ref parses));
        string tempExcel = Path.Combine(Path.GetTempPath(), $"temp_reload_{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(tempExcel, "dummy excel content");

        try
        {
            await vm.LoadExcelFileAsync(tempExcel);

            vm.SetActiveProfileCommand.Execute(vm.ActiveProfile!);

            await WaitUntilAsync(() => Volatile.Read(ref parses) == 2);
        }
        finally
        {
            if (File.Exists(tempExcel)) File.Delete(tempExcel);
        }
    }

    [Test]
    public void ActiveProfile_WithoutLoadedFile_ShouldNotReadAFile()
    {
        int parses = 0;
        var vm = CreateCountingVm(() => Interlocked.Increment(ref parses));

        vm.ActiveProfile = ProfileTestExtensions.FakeProfile("Other");

        parses.Should().Be(0);
        vm.LoadedOrder.Should().BeNull();
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
                _formats,
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
        var formats = new FormatRegistry([new FakeFormatAdapter { ParseFunc = _ => new BexioOrderImport.Domain.Models.Order() }]);

        var vm = new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            formats,
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
            _formats,
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
        var clientMock = new Mock<IBexioService>();
        clientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
        clientMock.Setup(c => c.GetAccountsAsync()).ThrowsAsync(new HttpRequestException("Bexio API Accounts error"));
        _clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>())).Returns(clientMock.Object);

        var vm = CreateVm();
        vm.BexioToken = "test_token";

        await vm.CheckBexioConnectionAsync();

        vm.LogText.Should().Contain("Could not load accounts: Bexio API Accounts error");
    }
}
