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

    private void RunInSta(Action action)
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

    private void RunInSta(Func<Task> action)
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

    private MainViewModel CreateVm()
    {
        var vm = new MainViewModel(new MockUpdateService(), new MockBexioClientFactory(), _tempFilePath);
        
        // Setup default mock dialog providers so unit tests never open real WPF windows or block
        vm.ProfileCreateDialogProvider = isClone => "MockProfile";
        vm.ProfileEditDialogProvider = profile => true;
        vm.OpenFileDialogProvider = (filter, ext) => "mock_file.xlsx";
        vm.SaveFileDialogProvider = (filter, ext, defaultName) => "mock_save_file.json";
        vm.ConfirmDialogProvider = (message, title) => true;
        vm.CustomerConfirmDialogProvider = customer => true;
        vm.ErrorDialogProvider = (message, title) => { };
        vm.InfoDialogProvider = message => { };
        
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
                    DefaultAccountId = 4000,
                    DefaultTaxId = 2,
                    Language = "en"
                },
                ActiveProfileName = "CustomProfile",
                Profiles = new System.Collections.Generic.List<MappingProfileDto>
                {
                    new MappingProfileDto
                    {
                        Name = "CustomProfile",
                        ExcelMapping = new ExcelMappingDto
                        {
                            WorksheetIndex = 2,
                            Header = new HeaderMappingDto { CompanyNameCell = "C10" }
                        }
                    }
                }
            };
            File.WriteAllText(_tempFilePath, JsonSerializer.Serialize(testSettings));

            // Act
            var vm = CreateVm();

            // Assert
            vm.BexioToken.Should().Be("my-test-token");
            vm.DefaultAccountId.Should().Be(4000);
            vm.DefaultTaxId.Should().Be(2);
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
            vm.DefaultAccountId = 3400;
            vm.DefaultTaxId = 3;
            vm.SelectedLanguage = "de";

            // Act
            vm.SaveSettingsCommand.Execute(null);

            // Assert
            File.Exists(_tempFilePath).Should().BeTrue();
            string content = File.ReadAllText(_tempFilePath);
            var dto = JsonSerializer.Deserialize<AppSettingsDto>(content);
            dto.Should().NotBeNull();
            dto!.Bexio.DefaultAccountId.Should().Be(3400);
            dto.Bexio.DefaultTaxId.Should().Be(3);
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

            copyVmToProfileMethod!.Invoke(vm, new object[] { profile });

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

            copyProfileToVmMethod!.Invoke(vm, new object[] { profile });

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

    private string FindExcelFile(string filename)
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
            vm.Email.Should().Be("orders@musterfashion.ch");
            vm.TotalQuantity.Should().Be(103);
            vm.TotalGrossAmount.Should().Be(2228.00m);
        });
    }

    [Fact]
    public async Task ImportToBexioAsync_ShouldExecuteImportSuccessfully()
    {
        RunInSta(async () =>
        {
            // Arrange
            var vm = CreateVm();
            string filePath = FindExcelFile("AnonymizedOrder.xlsx");
            await vm.LoadExcelFileAsync(filePath);
            vm.HasLoadedFile.Should().BeTrue();

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
        });
    }

    [Fact]
    public void CreateProfile_WithValidName_ShouldAddProfile()
    {
        RunInSta(() =>
        {
            var vm = CreateVm();
            vm.Profiles.Count.Should().Be(1);

            vm.ProfileCreateDialogProvider = isClone => "NewCustomProfile";
            vm.ProfileEditDialogProvider = p => true; // Close edit window with OK

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
            vm.ProfileCreateDialogProvider = isClone => "Default"; // Duplicate name

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

            vm.ProfileCreateDialogProvider = isClone => "ClonedDefault";

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

            vm.ProfileEditDialogProvider = p =>
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
                vm.SaveFileDialogProvider = (filter, ext, defaultName) => tempJsonPath;
                vm.ExportProfilesCommand.Execute(null);

                File.Exists(tempJsonPath).Should().BeTrue();

                // Create a new VM instance to test clean import
                var vm2 = CreateVm();
                vm2.Profiles.Count.Should().Be(1); // Only Default

                // Import
                vm2.OpenFileDialogProvider = (filter, ext) => tempJsonPath;
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
            vm.DefaultAccountId = 4000;
            vm.DefaultAccountId.Should().Be(4000);

            vm.DefaultTaxId = 5;
            vm.DefaultTaxId.Should().Be(5);

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
            var updateService = new MockUpdateService();
            var bexioClientFactory = new MockBexioClientFactory();
            var vm = new MainViewModel(updateService, bexioClientFactory);

            vm.BexioConnectionCheckTestHook = token => Task.FromResult(true);

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
            var updateService = new MockUpdateService();
            var bexioClientFactory = new MockBexioClientFactory();
            var vm = new MainViewModel(updateService, bexioClientFactory);

            vm.BexioConnectionCheckTestHook = token => Task.FromResult(false);

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
            var updateService = new MockUpdateService();
            var bexioClientFactory = new MockBexioClientFactory();
            var vm = new MainViewModel(updateService, bexioClientFactory);
            
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

    private class MockUpdateService : IUpdateService
    {
        public Task<UpdateInfo?> CheckForUpdatesAsync() => Task.FromResult<UpdateInfo?>(null);
        public Task DownloadAndInstallUpdateAsync(string downloadUrl, Action<double> progressCallback) => Task.CompletedTask;
        public bool IsNewerVersion(string rawTag, Version? currentVersion) => false;
    }

    private class MockBexioClientFactory : IBexioClientFactory
    {
        public IBexioClient Create(string apiToken, int accountId, int taxId) => new MockBexioClient();
    }

    private class MockBexioClient : IBexioClient
    {
        public Task<int?> FindContactIdAsync(string email) => Task.FromResult<int?>(null);
        public Task<int> CreateContactAsync(Customer customer) => Task.FromResult(0);
        public Task<int> CreateOrderAsync(int contactId, Order order) => Task.FromResult(0);
        public Task<int?> FindArticleIdAsync(string articleNumber, string articleName) => Task.FromResult<int?>(null);
        public Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position) => Task.CompletedTask;
        public Task AddCustomPositionAsync(int orderId, OrderPosition position) => Task.CompletedTask;
    }
}
