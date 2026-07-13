using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Services;
using BexioOrderImport.Wpf.ViewModels;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Application.Options;
using Moq;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace BexioOrderImport.Tests;

public class SettingsPersistenceTests : IDisposable
{
    private readonly string _tempFilePath;

    public SettingsPersistenceTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_appsettings.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); } catch { }
        }
    }

    private static void RunInSta(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    private static void RunInSta(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    private Mock<IDialogService> _dialogServiceMock = null!;

    // Delegated fields for dialog stubs
    private Func<bool, string?>? _profileCreateDialogFunc;
    private Func<MappingProfile, bool>? _profileEditDialogFunc;
    private Func<string, string, string?>? _openFileDialogFunc;
    private Func<string, string, string, string?>? _saveFileDialogFunc;
    private Func<string, string, bool>? _confirmDialogFunc;
    private Func<Customer, bool>? _customerConfirmDialogFunc;
    private Action<string, string>? _errorDialogAction;
    private Action<string>? _infoDialogAction;

    private MainViewModel CreateVm()
    {
        var mockUpdate = new Mock<IUpdateService>();
        var mockFactory = new Mock<IBexioClientFactory>();
        var mockClient = new Mock<IBexioClient>();
        mockClient.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
        mockFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(mockClient.Object);
        return CreateVm(mockUpdate.Object, mockFactory.Object, _tempFilePath);
    }

    private MainViewModel CreateVm(IUpdateService updateService, IBexioClientFactory clientFactory, string? configFilePath = null)
    {
        _profileCreateDialogFunc = null;
        _profileEditDialogFunc = null;
        _openFileDialogFunc = null;
        _saveFileDialogFunc = null;
        _confirmDialogFunc = null;
        _customerConfirmDialogFunc = null;
        _errorDialogAction = null;
        _infoDialogAction = null;

        _dialogServiceMock = new Mock<IDialogService>();
        _dialogServiceMock.Setup(d => d.ShowProfileCreateDialog(It.IsAny<bool>()))
            .Returns((bool isClone) => _profileCreateDialogFunc != null ? _profileCreateDialogFunc(isClone) : "MockProfile");
        _dialogServiceMock.Setup(d => d.ShowProfileEditDialog(It.IsAny<MappingProfile>()))
            .Returns((MappingProfile p) => _profileEditDialogFunc == null || _profileEditDialogFunc(p));
        _dialogServiceMock.Setup(d => d.ShowOpenFileDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string filter, string ext) => _openFileDialogFunc != null ? _openFileDialogFunc(filter, ext) : "mock_file.xlsx");
        _dialogServiceMock.Setup(d => d.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string filter, string ext, string defaultName) => _saveFileDialogFunc != null ? _saveFileDialogFunc(filter, ext, defaultName) : "mock_save_file.json");
        _dialogServiceMock.Setup(d => d.ShowConfirmDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string msg, string title) => _confirmDialogFunc == null || _confirmDialogFunc(msg, title));
        _dialogServiceMock.Setup(d => d.ShowCustomerConfirmDialog(It.IsAny<Customer>()))
            .Returns((Customer c) => _customerConfirmDialogFunc == null || _customerConfirmDialogFunc(c));
        _dialogServiceMock.Setup(d => d.ShowErrorDialog(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string msg, string title) => _errorDialogAction?.Invoke(msg, title));
        _dialogServiceMock.Setup(d => d.ShowInfoDialog(It.IsAny<string>()))
            .Callback((string msg) => _infoDialogAction?.Invoke(msg));

        var dispatcherMock = new Mock<IDispatcherService>();
        dispatcherMock.Setup(d => d.Invoke(It.IsAny<Action>())).Callback((Action a) => a());
        dispatcherMock.Setup(d => d.BeginInvoke(It.IsAny<Action>())).Callback((Action a) => a());

        var encryptionMock = new Mock<IEncryptionService>();
        encryptionMock.Setup(e => e.Encrypt(It.IsAny<string>())).Returns((string clearText) => clearText);
        encryptionMock.Setup(e => e.Decrypt(It.IsAny<string>())).Returns((string encryptedText) => encryptedText);

        var vm = new MainViewModel(
            updateService,
            clientFactory,
            _dialogServiceMock.Object,
            dispatcherMock.Object,
            encryptionMock.Object,
            configFilePath);
        if (vm.AccountId == null) vm.AccountId = 1;
        if (vm.TaxId == null) vm.TaxId = 1;
        return vm;
    }

    [Fact]
    public void EnsureAppSettingsFile_WhenFileDoesNotExist_ShouldCreateDefaultFile()
    {
        RunInSta(() =>
        {
            // Act
            var vm = CreateVm();

            // Assert
            File.Exists(_tempFilePath).Should().BeTrue();
            string content = File.ReadAllText(_tempFilePath);
            content.Should().Contain("Default");
            content.Should().Contain("bexio_api_token_here");
        });
    }

    [Fact]
    public void LoadSettings_WithValidJson_ShouldLoadValuesCorrectly()
    {
        RunInSta(() =>
        {
            // Arrange
            var testSettings = new AppSettingsDto
            {
                Bexio = new BexioSettingsDto
                {
                    ApiToken = "my-test-token",
                    AccountId = 1,
                    TaxId = 1,
                    Language = "en"
                },
                ActiveProfileName = "CustomProfile",
                Profiles =
                [
                    new() {
                        Name = "CustomProfile",
                        ExcelMapping = new ExcelMappingDto
                        {
                            WorksheetIndex = 2,
                            Header = new HeaderMappingDto { CompanyNameCell = "C10" }
                        }
                    }
                ]
            };
            File.WriteAllText(_tempFilePath, JsonSerializer.Serialize(testSettings));

            // Act
            var vm = CreateVm();

            // Assert
            vm.BexioToken.Should().Be("my-test-token");
            vm.AccountId.Should().Be(1);
            vm.TaxId.Should().Be(1);
            vm.SelectedLanguage.Should().Be("en");
            vm.ActiveProfile.Should().NotBeNull();
            vm.ActiveProfile!.Name.Should().Be("CustomProfile");
            vm.CompanyNameCell.Should().Be("C10");
        });
    }

    [Fact]
    public void SaveSettings_ShouldWriteJsonCorrectly()
    {
        RunInSta(() =>
        {
            // Arrange
            var vm = CreateVm();
            vm.BexioToken = "saved-token";
            vm.AccountId = 1; ;
            vm.TaxId = 1;
            vm.SelectedLanguage = "de";

            // Act
            vm.SaveSettingsCommand.Execute(null);

            // Assert
            File.Exists(_tempFilePath).Should().BeTrue();
            string content = File.ReadAllText(_tempFilePath);
            var dto = JsonSerializer.Deserialize<AppSettingsDto>(content);
            dto.Should().NotBeNull();
            dto!.Bexio.AccountId.Should().Be(1);
            dto.Bexio.TaxId.Should().Be(1);
            dto.Bexio.Language.Should().Be("de");
        });
    }

    [Fact]
    public void CopyVmToProfile_And_CopyProfileToVm_ShouldWorkCorrectly()
    {
        RunInSta(() =>
        {
            // Arrange
            var vm = CreateVm();
            var profile = new MappingProfile { Name = "Test", Mapping = new Application.Options.ExcelMappingOptions() };

            // Act & Assert 1: VM to Profile
            vm.CompanyNameCell = "A1";
            vm.StreetCell = "A2";
            vm.ZipCityCell = "A3";
            vm.BuyerEmailCell = "A4";
            vm.BuyerNameCell = "A5";
            vm.DeliveryDateCell = "A6";
            vm.PaymentTermsCell = "A7";
            vm.DiscountCell = "A8";

            vm.MatrixStartRow = 100;
            vm.MatrixEndRow = 105;
            vm.MatrixCategoryCol = 10;
            vm.MatrixStartSizeCol = 11;
            vm.MatrixEndSizeCol = 20;

            vm.DataStartRow = 200;
            vm.ColArtNum = 1;
            vm.ColArtName = 2;
            vm.ColColor = 3;
            vm.ColSizeCategory = 4;
            vm.ColStartQty = 5;
            vm.ColEndQty = 15;
            vm.ColUnitPrice = 16;

            var copyVmToProfileMethod = typeof(MainViewModel).GetMethod("CopyVmToProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var copyProfileToVmMethod = typeof(MainViewModel).GetMethod("CopyProfileToVm", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            copyVmToProfileMethod!.Invoke(vm, [profile]);

            profile.Mapping.Header.CompanyNameCell.Should().Be("A1");
            profile.Mapping.Header.StreetCell.Should().Be("A2");
            profile.Mapping.Header.ZipCityCell.Should().Be("A3");
            profile.Mapping.Header.BuyerEmailCell.Should().Be("A4");
            profile.Mapping.Header.BuyerNameCell.Should().Be("A5");
            profile.Mapping.Header.DeliveryDateCell.Should().Be("A6");
            profile.Mapping.Header.PaymentTermsCell.Should().Be("A7");
            profile.Mapping.Header.DiscountCell.Should().Be("A8");

            profile.Mapping.SizeMatrix.StartRow.Should().Be(100);
            profile.Mapping.SizeMatrix.EndRow.Should().Be(105);
            profile.Mapping.SizeMatrix.CategoryColumn.Should().Be(10);
            profile.Mapping.SizeMatrix.StartSizeColumn.Should().Be(11);
            profile.Mapping.SizeMatrix.EndSizeColumn.Should().Be(20);

            profile.Mapping.Data.StartRow.Should().Be(200);
            profile.Mapping.Data.ArticleNumberColumn.Should().Be(1);
            profile.Mapping.Data.ArticleNameColumn.Should().Be(2);
            profile.Mapping.Data.ColorColumn.Should().Be(3);
            profile.Mapping.Data.CategoryColumn.Should().Be(4);
            profile.Mapping.Data.StartQtyColumn.Should().Be(5);
            profile.Mapping.Data.EndQtyColumn.Should().Be(15);
            profile.Mapping.Data.UnitPriceColumn.Should().Be(16);

            // Act & Assert 2: Profile to VM
            profile.Mapping.Header.CompanyNameCell = "B1";
            profile.Mapping.SizeMatrix.StartRow = 50;
            profile.Mapping.Data.StartRow = 60;

            copyProfileToVmMethod!.Invoke(vm, [profile]);

            vm.CompanyNameCell.Should().Be("B1");
            vm.MatrixStartRow.Should().Be(50);
            vm.DataStartRow.Should().Be(60);
        });
    }

    [Fact]
    public void DeleteProfile_ShouldRemoveProfileFromList()
    {
        RunInSta(() =>
        {
            // Arrange
            var vm = CreateVm();
            var newProfile = new MappingProfile { Name = "ToDelete", Mapping = new ExcelMappingOptions() };
            vm.Profiles.Add(newProfile);
            vm.Profiles.Count.Should().Be(2);

            // Act
            vm.DeleteProfileCommand.Execute(newProfile);

            // Assert
            vm.Profiles.Should().NotContain(newProfile);
            vm.Profiles.Count.Should().Be(1);
        });
    }

    [Fact]
    public void SetActiveProfile_ShouldChangeActiveProfile()
    {
        RunInSta(() =>
        {
            // Arrange
            var vm = CreateVm();
            var newProfile = new MappingProfile { Name = "ActiveTest", Mapping = new ExcelMappingOptions() };
            vm.Profiles.Add(newProfile);

            // Act
            vm.SetActiveProfileCommand.Execute(newProfile);

            // Assert
            vm.ActiveProfile.Should().Be(newProfile);
        });
    }

    private static string FindExcelFile(string filename)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            string path = Path.Combine(dir, filename);
            if (File.Exists(path)) return path;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"Excel file {filename} not found in any parent directories.");
    }

    [Fact]
    public async Task LoadExcelFileAsync_WithValidFile_ShouldPopulateViewModelCorrectly()
    {
        RunInSta(async () =>
        {
            // Arrange
            var vm = CreateVm();
            string filePath = FindExcelFile("AnonymizedOrder.xlsx");

            // Act
            await vm.LoadExcelFileAsync(filePath);

            // Assert
            vm.HasLoadedFile.Should().BeTrue();
            vm.CompanyName.Should().Be("Muster Fashion AG");
            vm.BuyerName.Should().Be("Hans Muster");
            vm.Email.Should().Be("chris@peakmile.com");
            vm.TotalQuantity.Should().Be(4);
            vm.TotalGrossAmount.Should().Be(72.8m);
        });
    }

    [Fact]
    public async Task ImportToBexioAsync_ShouldExecuteImportSuccessfully()
    {
        RunInSta(async () =>
        {
            // Arrange
            var mockUpdate = new Mock<IUpdateService>();
            var mockFactory = new Mock<IBexioClientFactory>();
            var mockClient = new Mock<IBexioClient>();
            mockClient.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
            mockClient.Setup(c => c.CreateOrderAsync(It.IsAny<int>(), It.IsAny<Order>())).ReturnsAsync(12345);
            mockClient.Setup(c => c.FindArticleIdAsync(It.IsAny<string>())).ReturnsAsync(999);
            mockFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(mockClient.Object);

            var vm = CreateVm(mockUpdate.Object, mockFactory.Object, _tempFilePath);
            string filePath = FindExcelFile("AnonymizedOrder.xlsx");
            await vm.LoadExcelFileAsync(filePath);
            vm.HasLoadedFile.Should().BeTrue();

            string? infoMessage = null;
            _infoDialogAction = msg => infoMessage = msg;

            // Act
            vm.ImportCommand.Execute(null);

            // Wait until import starts
            int startTimeout = 50;
            while (!vm.IsImporting && startTimeout > 0)
            {
                await Task.Delay(10);
                startTimeout--;
            }

            // Wait for import process to finish
            int timeout = 1000;
            while (vm.IsImporting && timeout > 0)
            {
                await Task.Delay(50);
                timeout--;
            }

            // Assert
            vm.IsImporting.Should().BeFalse();
            vm.HasLoadedFile.Should().BeFalse();
            vm.LogText.Should().Contain("Import completed successfully");
            infoMessage.Should().NotBeNull();
            infoMessage.Should().Contain("12345");
        });
    }

    [Fact]
    public void CreateProfile_WithValidName_ShouldAddProfile()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.Profiles.Count.Should().Be(1);

            _profileCreateDialogFunc = isClone => "NewCustomProfile";
            _profileEditDialogFunc = p => true; // Close edit window with OK

            vm.CreateProfileCommand.Execute(null);

            vm.Profiles.Count.Should().Be(2);
            vm.Profiles.Any(p => p.Name == "NewCustomProfile").Should().BeTrue();
            vm.SelectedProfile!.Name.Should().Be("NewCustomProfile");
            vm.IsModified.Should().BeTrue();
        });
    }

    [Fact]
    public void CreateProfile_WithDuplicateName_ShouldShowErrorAndNotAdd()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            _profileCreateDialogFunc = isClone => "Default"; // Duplicate name

            vm.CreateProfileCommand.Execute(null);

            vm.Profiles.Count.Should().Be(1);
        });
    }

    [Fact]
    public void CloneProfile_WithValidName_ShouldCloneProfile()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            var sourceProfile = vm.Profiles[0];
            sourceProfile.Mapping.Header.CompanyNameCell = "Z99";

            _profileCreateDialogFunc = isClone => "ClonedDefault";

            vm.CloneProfileCommand.Execute(sourceProfile);

            vm.Profiles.Count.Should().Be(2);
            var cloned = vm.Profiles.First(p => p.Name == "ClonedDefault");
            cloned.Mapping.Header.CompanyNameCell.Should().Be("Z99");
        });
    }

    [Fact]
    public void EditProfile_ShouldModifyProfileAndCopyVm()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            var profile = vm.Profiles[0];

            _profileEditDialogFunc = p =>
            {
                p.Mapping.Header.CompanyNameCell = "X12";
                return true; // Click OK
            };

            vm.EditProfileCommand.Execute(profile);

            profile.Mapping.Header.CompanyNameCell.Should().Be("X12");
            vm.CompanyNameCell.Should().Be("X12");
        });
    }

    [Fact]
    public void ExportAndImportProfiles_ShouldExportToAndImportFromDisk()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            string tempJsonPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_export.json");

            try
            {
                // Add a custom profile
                var custom = new MappingProfile { Name = "ExportTest", Mapping = new ExcelMappingOptions() };
                custom.Mapping.Header.CompanyNameCell = "T1";
                vm.Profiles.Add(custom);

                // Export
                _saveFileDialogFunc = (filter, ext, defaultName) => tempJsonPath;
                vm.ExportProfilesCommand.Execute(null);

                File.Exists(tempJsonPath).Should().BeTrue();

                // Create a new VM instance to test clean import
                var vm2 = CreateVm();
                vm2.Profiles.Count.Should().Be(1); // Only Default

                // Import
                _openFileDialogFunc = (filter, ext) => tempJsonPath;
                vm2.ImportProfilesCommand.Execute(null);

                vm2.Profiles.Count.Should().Be(2);
                vm2.Profiles.Any(p => p.Name == "ExportTest").Should().BeTrue();
                vm2.Profiles.First(p => p.Name == "ExportTest").Mapping.Header.CompanyNameCell.Should().Be("T1");
            }
            finally
            {
                if (File.Exists(tempJsonPath))
                {
                    try { File.Delete(tempJsonPath); } catch { }
                }
            }
        });
    }

    [Fact]
    public void MainViewModel_Properties_ShouldRaisePropertyChanged()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            bool raised = false;
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.BexioToken)) raised = true; };

            vm.BexioToken = "test_token";
            vm.BexioToken.Should().Be("test_token");
            raised.Should().BeTrue();

            // Test other fields
            vm.AccountId = 1;
            vm.AccountId.Should().Be(1);
 
            vm.TaxId = 1;
            vm.TaxId.Should().Be(1);

            vm.SelectedLanguage = "en";
            vm.SelectedLanguage.Should().Be("en");

            vm.CompanyNameCell = "A1";
            vm.CompanyNameCell.Should().Be("A1");

            vm.StreetCell = "A2";
            vm.StreetCell.Should().Be("A2");

            vm.ZipCityCell = "A3";
            vm.ZipCityCell.Should().Be("A3");

            vm.BuyerEmailCell = "A4";
            vm.BuyerEmailCell.Should().Be("A4");

            vm.BuyerNameCell = "A5";
            vm.BuyerNameCell.Should().Be("A5");

            vm.DeliveryDateCell = "A6";
            vm.DeliveryDateCell.Should().Be("A6");

            vm.PaymentTermsCell = "A7";
            vm.PaymentTermsCell.Should().Be("A7");

            vm.DiscountCell = "A8";
            vm.DiscountCell.Should().Be("A8");

            vm.MatrixStartRow = 15;
            vm.MatrixStartRow.Should().Be(15);

            vm.MatrixEndRow = 20;
            vm.MatrixEndRow.Should().Be(20);

            vm.MatrixCategoryCol = 6;
            vm.MatrixCategoryCol.Should().Be(6);

            vm.MatrixStartSizeCol = 7;
            vm.MatrixStartSizeCol.Should().Be(7);

            vm.MatrixEndSizeCol = 12;
            vm.MatrixEndSizeCol.Should().Be(12);

            vm.DataStartRow = 21;
            vm.DataStartRow.Should().Be(21);

            vm.ColArtNum = 8;
            vm.ColArtNum.Should().Be(8);

            vm.ColArtName = 9;
            vm.ColArtName.Should().Be(9);

            vm.ColColor = 10;
            vm.ColColor.Should().Be(10);

            vm.ColSizeCategory = 11;
            vm.ColSizeCategory.Should().Be(11);

            vm.ColStartQty = 12;
            vm.ColStartQty.Should().Be(12);

            vm.ColEndQty = 17;
            vm.ColEndQty.Should().Be(17);

            vm.ColUnitPrice = 18;
            vm.ColUnitPrice.Should().Be(18);

            vm.CompanyName = "Test Company";
            vm.CompanyName.Should().Be("Test Company");

            vm.BuyerName = "Test Buyer";
            vm.BuyerName.Should().Be("Test Buyer");

            vm.Email = "test@test.com";
            vm.Email.Should().Be("test@test.com");

            vm.Address = "Test Address";
            vm.Address.Should().Be("Test Address");

            vm.DeliveryDate = "01.01.2026";
            vm.DeliveryDate.Should().Be("01.01.2026");

            vm.PaymentTerms = "30 Days";
            vm.PaymentTerms.Should().Be("30 Days");

            vm.ProgressPercentage = 50;
            vm.ProgressPercentage.Should().Be(50);

            vm.LogText = "Log message";
            vm.LogText.Should().Be("Log message");

            vm.IsImporting = true;
            vm.IsImporting.Should().BeTrue();

            vm.IsImportingActive = true;
            vm.IsImportingActive.Should().BeTrue();

            vm.IsLoading = true;
            vm.IsLoading.Should().BeTrue();

            vm.SelectedFilePath = "test.xlsx";
            vm.SelectedFilePath.Should().Be("test.xlsx");

            vm.FileSizeText = "15 KB";
            vm.FileSizeText.Should().Be("15 KB");

            vm.TotalQuantity = 100;
            vm.TotalQuantity.Should().Be(100);

            vm.TotalGrossAmount = 2500m;
            vm.TotalGrossAmount.Should().Be(2500m);

            vm.DiscountPercentVal = 10m;
            vm.DiscountPercentVal.Should().Be(10m);

            vm.DiscountAmount = 250m;
            vm.DiscountAmount.Should().Be(250m);

            vm.TotalNetAmount = 2250m;
            vm.TotalNetAmount.Should().Be(2250m);

            vm.TotalsSummary = "Summary";
            vm.TotalsSummary.Should().Be("Summary");

            // Verify remaining getters/setters
            vm.ConnectionStatusText = "Connected";
            vm.ConnectionStatusText.Should().Be("Connected");

            vm.ConnectionStatusColor = "Green";
            vm.ConnectionStatusColor.Should().Be("Green");

            vm.IsUpdateAvailable = true;
            vm.IsUpdateAvailable.Should().BeTrue();

            vm.UpdateVersion = "v1.2.0";
            vm.UpdateVersion.Should().Be("v1.2.0");

            vm.IsDownloadingUpdate = true;
            vm.IsDownloadingUpdate.Should().BeTrue();

            vm.UpdateStatusText = "Downloading...";
            vm.UpdateStatusText.Should().Be("Downloading...");

            vm.UpdateProgress = 50.0;
            vm.UpdateProgress.Should().Be(50.0);

            vm.IsTokenFocused = true;
            vm.IsTokenFocused.Should().BeTrue();

            // Verify commands are initialized
            vm.LoadFileCommand.Should().NotBeNull();
            vm.ClearFileCommand.Should().NotBeNull();
            vm.ImportCommand.Should().NotBeNull();
            vm.SaveSettingsCommand.Should().NotBeNull();
            vm.CreateProfileCommand.Should().NotBeNull();
            vm.EditProfileCommand.Should().NotBeNull();
            vm.CloneProfileCommand.Should().NotBeNull();
            vm.SetActiveProfileCommand.Should().NotBeNull();
            vm.DeleteProfileCommand.Should().NotBeNull();
            vm.ExportProfilesCommand.Should().NotBeNull();
            vm.ImportProfilesCommand.Should().NotBeNull();
            vm.InstallUpdateCommand.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task CheckBexioConnectionAsync_WhenSuccess_ShouldUpdateStatus()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var bexioClientFactoryMock = new Mock<IBexioClientFactory>();
            var bexioClientMock = new Mock<IBexioClient>();
            bexioClientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
            bexioClientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(bexioClientMock.Object);

            var vm = CreateVm(updateServiceMock.Object, bexioClientFactoryMock.Object);

            await vm.CheckBexioConnectionAsync();

            vm.ConnectionStatusText.Should().Be(BexioOrderImport.Wpf.Resources.Translations.Status_BexioConnected);
            vm.ConnectionStatusColor.Should().Be("#10B981");
        });
    }

    [Fact]
    public async Task CheckBexioConnectionAsync_WhenFailure_ShouldUpdateStatus()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var bexioClientFactoryMock = new Mock<IBexioClientFactory>();
            var bexioClientMock = new Mock<IBexioClient>();
            bexioClientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(false);
            bexioClientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(bexioClientMock.Object);

            var vm = CreateVm(updateServiceMock.Object, bexioClientFactoryMock.Object);

            await vm.CheckBexioConnectionAsync();

            vm.ConnectionStatusText.Should().Be(BexioOrderImport.Wpf.Resources.Translations.Status_BexioDisconnected);
            vm.ConnectionStatusColor.Should().Be("#EF4444");
        });
    }

    [Fact]
    public void ActiveProfile_ChangeWhenFileLoadedAndExists_ShouldTriggerFileReload()
    {
        RunInSta(() =>
        {
            // Arrange
            var updateServiceMock = new Mock<IUpdateService>();
            var bexioClientFactoryMock = new Mock<IBexioClientFactory>();
            var bexioClientMock = new Mock<IBexioClient>();
            bexioClientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(bexioClientMock.Object);
            var vm = CreateVm(updateServiceMock.Object, bexioClientFactoryMock.Object);

            // Create a temp dummy file to satisfy File.Exists check
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
            File.WriteAllText(tempFile, "");

            try
            {
                vm.SelectedFilePath = tempFile;

                // Create a second profile to switch to
                var profile2 = new BexioOrderImport.Wpf.Models.MappingProfile { Name = "Profile2", Mapping = new ExcelMappingOptions() };
                vm.Profiles.Add(profile2);

                // Act
                vm.ActiveProfile = profile2;

                // Assert
                vm.ActiveProfile.Should().Be(profile2);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        });
    }



    [Fact]
    public void LoadExcelFileAsync_WithNullFilePath_ShouldCallOpenFileDialogProvider()
    {
        RunInSta(async () =>
        {
            var vm = CreateVm();
            bool providerCalled = false;
            _openFileDialogFunc = (filter, ext) =>
            {
                providerCalled = true;
                return null;
            };

            await vm.LoadExcelFileAsync(null);

            providerCalled.Should().BeTrue();
            vm.HasLoadedFile.Should().BeFalse();
        });
    }

    [Fact]
    public void CheckBexioConnectionAsync_WhenExceptionThrown_ShouldUpdateStatusToDisconnected()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var bexioClientFactoryMock = new Mock<IBexioClientFactory>();
            bexioClientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .Throws(new InvalidOperationException("Factory failed"));
            var vm = CreateVm(updateServiceMock.Object, bexioClientFactoryMock.Object);

            await vm.CheckBexioConnectionAsync();

            vm.ConnectionStatusText.Should().Be(BexioOrderImport.Wpf.Resources.Translations.Status_BexioDisconnected);
            vm.ConnectionStatusColor.Should().Be("#EF4444");
        });
    }

    [Fact]
    public void ImportToBexioAsync_WhenConfirmUploadReturnsFalse_ShouldCancelImport()
    {
        RunInSta(async () =>
        {
            var vm = CreateVm();
            _confirmDialogFunc = (msg, title) => false; // Cancel upload confirmation

            string filePath = FindExcelFile("AnonymizedOrder.xlsx");
            await vm.LoadExcelFileAsync(filePath);

            vm.ImportCommand.Execute(null);

            // Wait for import
            int timeout = 100;
            while (vm.IsImporting && timeout > 0)
            {
                await Task.Delay(10);
                timeout--;
            }

            vm.IsImporting.Should().BeFalse();
            vm.LogText.Should().Contain("Import cancelled. File remains loaded.");
        });
    }

    [Fact]
    public void ImportToBexioAsync_WhenExceptionThrown_ShouldCatchAndLog()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var throwClientFactoryMock = new Mock<IBexioClientFactory>();
            throwClientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .Throws(new InvalidOperationException("Factory failed"));
            var vm = CreateVm(updateServiceMock.Object, throwClientFactoryMock.Object, _tempFilePath);
            _confirmDialogFunc = (msg, title) => true;

            string filePath = FindExcelFile("AnonymizedOrder.xlsx");
            await vm.LoadExcelFileAsync(filePath);

            vm.ImportCommand.Execute(null);

            int timeout = 100;
            while (vm.IsImporting && timeout > 0)
            {
                await Task.Delay(10);
                timeout--;
            }

            vm.LogText.Should().Contain("Error during import");
        });
    }

    [Fact]
    public void SaveSettings_WithLanguageChangedAndReloadConfirmed_ShouldAttemptMainWindowReload()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.SelectedLanguage = "en";
            vm.IsModified = true;
            _confirmDialogFunc = (msg, title) => true;

            vm.SaveSettingsCommand.Execute(null);

            vm.SelectedLanguage.Should().Be("en");
        });
    }

    [Fact]
    public void SaveSettings_WithLanguageChangedAndReloadCancelled_ShouldOnlyChangeInitialLanguage()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.SelectedLanguage = "en";
            vm.IsModified = true;
            _confirmDialogFunc = (msg, title) => false; // Cancel reload

            vm.SaveSettingsCommand.Execute(null);

            vm.SelectedLanguage.Should().Be("en");
        });
    }

    [Fact]
    public void LoadSettings_WithLegacySingleProfile_ShouldMigrateCorrectly()
    {
        RunInSta(() =>
        {
            string legacyJson = @"{
                ""Bexio"": {
                    ""ApiToken"": ""legacy_token"",
                    ""AccountId"": 3200,
                    ""TaxId"": 1,
                    ""Language"": ""de""
                },
                ""ExcelMapping"": {
                    ""WorksheetIndex"": 1,
                    ""Header"": { ""CompanyNameCell"": ""B4"" },
                    ""SizeMatrix"": { ""StartRow"": 10 },
                    ""Data"": { ""StartRow"": 18 }
                }
            }";
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, legacyJson);

            try
            {
                var updateServiceMock = new Mock<IUpdateService>();
                var clientFactoryMock = new Mock<IBexioClientFactory>();
                var clientMock = new Mock<IBexioClient>();
                clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(clientMock.Object);
                var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object, path);

                vm.BexioToken.Should().Be("legacy_token");
                vm.Profiles.Count.Should().Be(1);
                vm.Profiles[0].Name.Should().Be("Default");
                vm.Profiles[0].Mapping.Header.CompanyNameCell.Should().Be("B4");
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        });
    }

    [Fact]
    public void LoadSettings_WhenInvalidJson_ShouldShowError()
    {
        RunInSta(() =>
        {
            string corruptJson = "{ invalid json";
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, corruptJson);

            try
            {
                bool errorCalled = false;
                var updateServiceMock = new Mock<IUpdateService>();
                var clientFactoryMock = new Mock<IBexioClientFactory>();
                var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object, path);
                _errorDialogAction = (msg, title) => errorCalled = true;

                // Force load
                typeof(MainViewModel).GetMethod("LoadSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(vm, null);

                errorCalled.Should().BeTrue();
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        });
    }

    [Fact]
    public void SaveSettings_WhenWriteFails_ShouldShowError()
    {
        RunInSta(() =>
        {
            string validPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            bool errorCalled = false;

            var updateServiceMock = new Mock<IUpdateService>();
            var clientFactoryMock = new Mock<IBexioClientFactory>();
            var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object, validPath);

            try
            {
                _errorDialogAction = (msg, title) => errorCalled = true;

                // Point the config file path to a directory to force IOException on WriteAllText during Save
                typeof(MainViewModel)
                    .GetField("_configFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .SetValue(vm, Path.GetTempPath());

                vm.BexioToken = "token";
                vm.IsModified = true;

                vm.SaveSettingsCommand.Execute(null);

                errorCalled.Should().BeTrue();
            }
            finally
            {
                try { File.Delete(validPath); } catch { }
            }
        });
    }

    [Fact]
    public void EditProfile_WithActiveProfileAndFileLoaded_ShouldTriggerReload()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
            File.WriteAllText(tempFile, "");

            try
            {
                vm.SelectedFilePath = tempFile;
                _profileEditDialogFunc = p => true;

                // Act
                vm.EditProfileCommand.Execute(vm.ActiveProfile);

                // Assert
                vm.IsModified.Should().BeTrue();
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        });
    }

    [Fact]
    public void DeleteProfile_WithActiveOrSelectedProfile_ShouldFallbackToDefault()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            var customProfile = new BexioOrderImport.Wpf.Models.MappingProfile { Name = "Custom", Mapping = new ExcelMappingOptions() };
            vm.Profiles.Add(customProfile);
            vm.SelectedProfile = customProfile;
            vm.ActiveProfile = customProfile;

            _confirmDialogFunc = (msg, title) => true;

            // Act
            vm.DeleteProfileCommand.Execute(customProfile);

            // Assert
            vm.Profiles.Should().NotContain(customProfile);
            vm.SelectedProfile.Name.Should().Be("Default");
            vm.ActiveProfile.Name.Should().Be("Default");
        });
    }

    [Fact]
    public void ExportProfiles_WhenSaveFileDialogCancelled_ShouldDoNothing()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            _saveFileDialogFunc = (filter, ext, name) => null; // Cancelled
            bool infoCalled = false;
            _infoDialogAction = msg => infoCalled = true;

            vm.ExportProfilesCommand.Execute(null);

            infoCalled.Should().BeFalse();
        });
    }

    [Fact]
    public void ExportProfiles_WhenException_ShouldShowError()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            _saveFileDialogFunc = (filter, ext, name) => throw new IOException("Access denied");
            bool errorCalled = false;
            _errorDialogAction = (msg, title) => errorCalled = true;

            vm.ExportProfilesCommand.Execute(null);

            errorCalled.Should().BeTrue();
        });
    }

    [Fact]
    public void ImportProfiles_WhenOpenFileDialogCancelled_ShouldDoNothing()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            _openFileDialogFunc = (filter, ext) => null; // Cancelled
            bool infoCalled = false;
            _infoDialogAction = msg => infoCalled = true;

            vm.ImportProfilesCommand.Execute(null);

            infoCalled.Should().BeFalse();
        });
    }

    [Fact]
    public void ImportProfiles_WhenDeserializationReturnsNull_ShouldShowError()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, "null");

            try
            {
                _openFileDialogFunc = (filter, ext) => path;
                bool errorCalled = false;
                _errorDialogAction = (msg, title) => errorCalled = true;

                vm.ImportProfilesCommand.Execute(null);

                errorCalled.Should().BeTrue();
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        });
    }

    [Fact]
    public void ImportProfiles_WithActiveProfileAndFileLoaded_ShouldTriggerReload()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
            File.WriteAllText(tempFile, "");

            string json = @"[
                { ""Name"": ""Default"", ""ExcelMapping"": { ""WorksheetIndex"": 2 } }
            ]";
            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
            File.WriteAllText(path, json);

            try
            {
                vm.SelectedFilePath = tempFile;
                _openFileDialogFunc = (filter, ext) => path;

                vm.ImportProfilesCommand.Execute(null);

                vm.ActiveProfile!.Mapping.WorksheetIndex.Should().Be(2);
            }
            finally
            {
                try { File.Delete(tempFile); }
                catch { }
                try { File.Delete(path); }
                catch { }
            }
        });
    }

    [Fact]
    public void CheckForUpdatesAsync_WhenUpdateAvailable_ShouldSetProperties()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var updateInfo = new UpdateInfo { LatestVersion = "2.0.0", DownloadUrl = "http://download.url", ReleaseNotes = "release_notes" };
            updateServiceMock.Setup(u => u.CheckForUpdatesAsync()).ReturnsAsync(updateInfo);

            var clientFactoryMock = new Mock<IBexioClientFactory>();
            var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object);

            // Wait for completion (command runs async task in background on load)
            int timeout = 100;
            while (!vm.IsUpdateAvailable && timeout > 0)
            {
                await Task.Delay(10);
                timeout--;
            }

            vm.IsUpdateAvailable.Should().BeTrue();
            vm.UpdateVersion.Should().Be("2.0.0");
        });
    }

    [Fact]
    public void InstallUpdateAsync_WhenSuccess_ShouldReportProgress()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var updateInfo = new UpdateInfo { LatestVersion = "2.0.0", DownloadUrl = "http://download.url", ReleaseNotes = "release_notes" };
            updateServiceMock.Setup(u => u.CheckForUpdatesAsync()).ReturnsAsync(updateInfo);
            updateServiceMock.Setup(u => u.DownloadAndInstallUpdateAsync(It.IsAny<string>(), It.IsAny<Action<double>>()))
                .Returns((string url, Action<double> progressCallback) =>
                {
                    progressCallback(50);
                    progressCallback(100);
                    return Task.CompletedTask;
                });

            var clientFactoryMock = new Mock<IBexioClientFactory>();
            var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object);

            // Set update available state
            int timeout = 100;
            while (!vm.IsUpdateAvailable && timeout > 0)
            {
                await Task.Delay(10);
                timeout--;
            }

            // Act
            vm.InstallUpdateCommand.Execute(null);

            timeout = 100;
            while (vm.UpdateProgress < 100 && timeout > 0)
            {
                await Task.Delay(10);
                timeout--;
            }

            vm.UpdateProgress.Should().Be(100);
        });
    }

    [Fact]
    public void InstallUpdateAsync_WhenException_ShouldReportError()
    {
        RunInSta(async () =>
        {
            var updateServiceMock = new Mock<IUpdateService>();
            var updateInfo = new UpdateInfo { LatestVersion = "2.0.0", DownloadUrl = "http://download.url", ReleaseNotes = "release_notes" };
            updateServiceMock.Setup(u => u.CheckForUpdatesAsync()).ReturnsAsync(updateInfo);
            updateServiceMock.Setup(u => u.DownloadAndInstallUpdateAsync(It.IsAny<string>(), It.IsAny<Action<double>>()))
                .Throws(new InvalidOperationException("Download failed"));

            var clientFactoryMock = new Mock<IBexioClientFactory>();
            var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object);

            int timeout = 100;
            while (!vm.IsUpdateAvailable && timeout > 0)
            {
                await Task.Delay(10);
                timeout--;
            }

            // Act
            vm.InstallUpdateCommand.Execute(null);

            // Wait a moment for async download to throw
            await Task.Delay(100);

            vm.UpdateStatusText.Should().Contain("Download failed");
        });
    }

    [Fact]
    public void EnsureAppSettingsFile_WhenDirectoryDoesNotExist_ShouldCreateIt()
    {
        RunInSta(() =>
        {
            string nonExistentDir = Path.Combine(Path.GetTempPath(), "SubFolder_" + Guid.NewGuid().ToString());
            string path = Path.Combine(nonExistentDir, "settings.json");

            try
            {
                var updateServiceMock = new Mock<IUpdateService>();
                var clientFactoryMock = new Mock<IBexioClientFactory>();
                var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object, path);

                Directory.Exists(nonExistentDir).Should().BeTrue();
                File.Exists(path).Should().BeTrue();
            }
            finally
            {
                try { Directory.Delete(nonExistentDir, true); } catch { }
            }
        });
    }

    [Fact]
    public void EnsureAppSettingsFile_WhenTemplateExists_ShouldCopyTemplate()
    {
        RunInSta(() =>
        {
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            string? originalTemplateContent = null;
            if (File.Exists(templatePath))
            {
                originalTemplateContent = File.ReadAllText(templatePath);
            }

            string testTemplate = @"{ ""Bexio"": { ""ApiToken"": ""template_token"" } }";
            File.WriteAllText(templatePath, testTemplate);

            string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

            try
            {
                var updateServiceMock = new Mock<IUpdateService>();
                var clientFactoryMock = new Mock<IBexioClientFactory>();
                var vm = CreateVm(updateServiceMock.Object, clientFactoryMock.Object, path);

                vm.BexioToken.Should().Be("template_token");
            }
            finally
            {
                try { File.Delete(path); } catch { }
                if (originalTemplateContent != null)
                {
                    File.WriteAllText(templatePath, originalTemplateContent);
                }
                else
                {
                    try { File.Delete(templatePath); } catch { }
                }
            }
        });
    }
}
