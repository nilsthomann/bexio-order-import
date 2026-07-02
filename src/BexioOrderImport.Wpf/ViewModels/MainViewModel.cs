using System;
using System.IO;
using System.Text.Json;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Options;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Infrastructure.Excel;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Resources;
using BexioOrderImport.Wpf.Views;

namespace BexioOrderImport.Wpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly string _configFilePath;
    private Order? _loadedOrder;
    private string _connectionStatusText = Translations.Status_BexioDisconnected;
    private string _connectionStatusColor = "#EF4444"; // Red
    private double _progressPercentage;
    private string _logText = string.Empty;
    private bool _isImporting;
    private bool _isImportingActive;
    private bool _isLoading;
    private string? _selectedFilePath;
    private bool _hasLoadedFile;
    private string _selectedFileName = string.Empty;
    private string _fileSizeText = string.Empty;
    private string _selectedLanguage = "de";
    private string _initialLanguage = "de";
    private Models.MappingProfile? _selectedProfile;
    private Models.MappingProfile? _activeProfile;

    private readonly Services.UpdateService _updateService = new();
    private string _updateDownloadUrl = string.Empty;
    private bool _isUpdateAvailable;
    private string _updateVersion = string.Empty;
    private bool _isDownloadingUpdate;
    private string _updateStatusText = string.Empty;
    private double _updateProgress;

    private int _totalQuantity;
    private decimal _totalGrossAmount;
    private decimal _discountPercentVal;
    private decimal _discountAmount;
    private decimal _totalNetAmount;

    // Excel Order header properties (bound to UI fields)
    private string _companyName = string.Empty;
    private string _buyerName = string.Empty;
    private string _email = string.Empty;
    private string _address = string.Empty;
    private string _deliveryDate = string.Empty;
    private string _paymentTerms = string.Empty;
    private string _totalsSummary = string.Empty;

    public bool HasLoadedFile
    {
        get => _hasLoadedFile;
        set => SetProperty(ref _hasLoadedFile, value);
    }

    public string SelectedFileName
    {
        get => _selectedFileName;
        set => SetProperty(ref _selectedFileName, value);
    }

    public string FileSizeText
    {
        get => _fileSizeText;
        set => SetProperty(ref _fileSizeText, value);
    }

    public int TotalQuantity
    {
        get => _totalQuantity;
        set => SetProperty(ref _totalQuantity, value);
    }

    public decimal TotalGrossAmount
    {
        get => _totalGrossAmount;
        set => SetProperty(ref _totalGrossAmount, value);
    }

    public decimal DiscountPercentVal
    {
        get => _discountPercentVal;
        set => SetProperty(ref _discountPercentVal, value);
    }

    public decimal DiscountAmount
    {
        get => _discountAmount;
        set => SetProperty(ref _discountAmount, value);
    }

    public decimal TotalNetAmount
    {
        get => _totalNetAmount;
        set => SetProperty(ref _totalNetAmount, value);
    }

    // Settings fields (bound to Settings Tab fields)
    private string _bexioToken = string.Empty;
    private int _defaultAccountId = 3200;
    private int _defaultTaxId = 1;
    
    private string _companyNameCell = "B4";
    private string _streetCell = "B5";
    private string _zipCityCell = "B6";
    private string _buyerEmailCell = "E5";
    private string _buyerNameCell = "E4";
    private string _deliveryDateCell = "T7";
    private string _paymentTermsCell = "A9";
    private string _discountCell = "V12";

    private int _matrixStartRow = 10;
    private int _matrixEndRow = 17;
    private int _matrixCategoryCol = 4;
    private int _matrixStartSizeCol = 5;
    private int _matrixEndSizeCol = 18;

    private int _dataStartRow = 18;
    private int _colArtNum = 1;
    private int _colArtName = 2;
    private int _colColor = 3;
    private int _colSizeCategory = 4;
    private int _colStartQty = 5;
    private int _colEndQty = 18;
    private int _colUnitPrice = 20;

    public MainViewModel()
    {
        // Commands
        LoadFileCommand = new RelayCommand(async () => await LoadExcelFileAsync());
        ClearFileCommand = new RelayCommand(ClearLoadedFile);
        ImportCommand = new RelayCommand(async () => await ImportToBexioAsync(), () => _loadedOrder != null && !_isImporting);
        SaveSettingsCommand = new RelayCommand(SaveSettings, () => IsModified);
        CreateProfileCommand = new RelayCommand(CreateProfile);
        EditProfileCommand = new RelayCommand<Models.MappingProfile>(EditProfile);
        CloneProfileCommand = new RelayCommand<Models.MappingProfile>(CloneProfile);
        SetActiveProfileCommand = new RelayCommand<Models.MappingProfile>(SetActiveProfile);
        DeleteProfileCommand = new RelayCommand<Models.MappingProfile>(DeleteProfile, p => p != null && p.Name != "Default");
        ExportProfilesCommand = new RelayCommand(ExportProfiles);
        ImportProfilesCommand = new RelayCommand(ImportProfiles);
        InstallUpdateCommand = new RelayCommand(async () => await InstallUpdateAsync(), () => !string.IsNullOrEmpty(_updateDownloadUrl) && !_isDownloadingUpdate);

        // Path to CLI appsettings.json or WPF appsettings.json.
        // We will store settings in user LocalAppData so updates do not delete them.
        string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        _configFilePath = Path.Combine(appDataFolder, "appsettings.json");
        
        // Copy appsettings.json from CLI directory if exists, or write default
        EnsureAppSettingsFile();

        LoadSettings();

        // Async check connection
        _ = CheckBexioConnectionAsync();

        // Async check for updates
        _ = CheckForUpdatesAsync();
    }

    // Commands
    public RelayCommand LoadFileCommand { get; }
    public RelayCommand ClearFileCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand CreateProfileCommand { get; }
    public RelayCommand<Models.MappingProfile> EditProfileCommand { get; }
    public RelayCommand<Models.MappingProfile> CloneProfileCommand { get; }
    public RelayCommand<Models.MappingProfile> SetActiveProfileCommand { get; }
    public RelayCommand<Models.MappingProfile> DeleteProfileCommand { get; }
    public RelayCommand ExportProfilesCommand { get; }
    public RelayCommand ImportProfilesCommand { get; }
    public RelayCommand InstallUpdateCommand { get; }

    // Properties for UI
    public ObservableCollection<OrderPosition> OrderPositions { get; } = new();

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        set => SetProperty(ref _connectionStatusText, value);
    }

    public string ConnectionStatusColor
    {
        get => _connectionStatusColor;
        set => SetProperty(ref _connectionStatusColor, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        set
        {
            if (SetProperty(ref _isImporting, value))
            {
                ImportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsImportingActive
    {
        get => _isImportingActive;
        set => SetProperty(ref _isImportingActive, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    public string UpdateVersion
    {
        get => _updateVersion;
        set => SetProperty(ref _updateVersion, value);
    }

    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set => SetProperty(ref _isDownloadingUpdate, value);
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetProperty(ref _updateStatusText, value);
    }

    public double UpdateProgress
    {
        get => _updateProgress;
        set => SetProperty(ref _updateProgress, value);
    }

    public string AppVersion => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public string? SelectedFilePath
    {
        get => _selectedFilePath;
        set => SetProperty(ref _selectedFilePath, value);
    }

    // Order properties
    public string CompanyName
    {
        get => _companyName;
        set => SetProperty(ref _companyName, value);
    }

    public string BuyerName
    {
        get => _buyerName;
        set => SetProperty(ref _buyerName, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string DeliveryDate
    {
        get => _deliveryDate;
        set => SetProperty(ref _deliveryDate, value);
    }

    public string PaymentTerms
    {
        get => _paymentTerms;
        set => SetProperty(ref _paymentTerms, value);
    }

    public string TotalsSummary
    {
        get => _totalsSummary;
        set => SetProperty(ref _totalsSummary, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<Models.MappingProfile> Profiles { get; } = new();

    public Models.MappingProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile != value)
            {
                if (_selectedProfile != null)
                {
                    CopyVmToProfile(_selectedProfile);
                }
                SetProperty(ref _selectedProfile, value);
                if (_selectedProfile != null)
                {
                    CopyProfileToVm(_selectedProfile);
                }
                DeleteProfileCommand.RaiseCanExecuteChanged();
                SetActiveProfileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public Models.MappingProfile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            SetProperty(ref _activeProfile, value);
            // Trigger reload of loaded file if active profile changes
            if (_activeProfile != null && !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }
        }
    }


    private bool _isModified;
    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    private void SetModified()
    {
        IsModified = true;
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    // Settings view bindings
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                SetModified();
            }
        }
    }

    public string BexioToken
    {
        get => _bexioToken;
        set
        {
            if (SetProperty(ref _bexioToken, value))
            {
                SetModified();
                OnPropertyChanged(nameof(BexioTokenDisplay));
            }
        }
    }

    private bool _isTokenFocused;
    public bool IsTokenFocused
    {
        get => _isTokenFocused;
        set
        {
            if (SetProperty(ref _isTokenFocused, value))
            {
                OnPropertyChanged(nameof(BexioTokenDisplay));
            }
        }
    }

    public string BexioTokenDisplay
    {
        get
        {
            if (_isTokenFocused)
            {
                return BexioToken;
            }
            else
            {
                return string.IsNullOrEmpty(BexioToken) ? string.Empty : new string('•', 24);
            }
        }
        set
        {
            if (_isTokenFocused)
            {
                BexioToken = value;
            }
            OnPropertyChanged(nameof(BexioTokenDisplay));
        }
    }

    public int DefaultAccountId
    {
        get => _defaultAccountId;
        set
        {
            if (SetProperty(ref _defaultAccountId, value))
            {
                SetModified();
            }
        }
    }

    public int DefaultTaxId
    {
        get => _defaultTaxId;
        set
        {
            if (SetProperty(ref _defaultTaxId, value))
            {
                SetModified();
            }
        }
    }

    public string CompanyNameCell
    {
        get => _companyNameCell;
        set => SetProperty(ref _companyNameCell, value);
    }

    public string StreetCell
    {
        get => _streetCell;
        set => SetProperty(ref _streetCell, value);
    }

    public string ZipCityCell
    {
        get => _zipCityCell;
        set => SetProperty(ref _zipCityCell, value);
    }

    public string BuyerEmailCell
    {
        get => _buyerEmailCell;
        set => SetProperty(ref _buyerEmailCell, value);
    }

    public string BuyerNameCell
    {
        get => _buyerNameCell;
        set => SetProperty(ref _buyerNameCell, value);
    }

    public string DeliveryDateCell
    {
        get => _deliveryDateCell;
        set => SetProperty(ref _deliveryDateCell, value);
    }

    public string PaymentTermsCell
    {
        get => _paymentTermsCell;
        set => SetProperty(ref _paymentTermsCell, value);
    }

    public string DiscountCell
    {
        get => _discountCell;
        set => SetProperty(ref _discountCell, value);
    }

    public int MatrixStartRow
    {
        get => _matrixStartRow;
        set => SetProperty(ref _matrixStartRow, value);
    }

    public int MatrixEndRow
    {
        get => _matrixEndRow;
        set => SetProperty(ref _matrixEndRow, value);
    }

    public int MatrixCategoryCol
    {
        get => _matrixCategoryCol;
        set => SetProperty(ref _matrixCategoryCol, value);
    }

    public int MatrixStartSizeCol
    {
        get => _matrixStartSizeCol;
        set => SetProperty(ref _matrixStartSizeCol, value);
    }

    public int MatrixEndSizeCol
    {
        get => _matrixEndSizeCol;
        set => SetProperty(ref _matrixEndSizeCol, value);
    }

    public int DataStartRow
    {
        get => _dataStartRow;
        set => SetProperty(ref _dataStartRow, value);
    }

    public int ColArtNum
    {
        get => _colArtNum;
        set => SetProperty(ref _colArtNum, value);
    }

    public int ColArtName
    {
        get => _colArtName;
        set => SetProperty(ref _colArtName, value);
    }

    public int ColColor
    {
        get => _colColor;
        set => SetProperty(ref _colColor, value);
    }

    public int ColSizeCategory
    {
        get => _colSizeCategory;
        set => SetProperty(ref _colSizeCategory, value);
    }

    public int ColStartQty
    {
        get => _colStartQty;
        set => SetProperty(ref _colStartQty, value);
    }

    public int ColEndQty
    {
        get => _colEndQty;
        set => SetProperty(ref _colEndQty, value);
    }

    public int ColUnitPrice
    {
        get => _colUnitPrice;
        set => SetProperty(ref _colUnitPrice, value);
    }

    // Methods
    public async Task CheckBexioConnectionAsync()
    {
        ConnectionStatusText = Translations.Status_BexioChecking;
        ConnectionStatusColor = "#F59E0B"; // Yellow warning
        
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BexioToken);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            
            var response = await client.GetAsync("https://office.bexio.com/api2.0/contact?limit=1");
            if (response.IsSuccessStatusCode)
            {
                ConnectionStatusText = Translations.Status_BexioConnected;
                ConnectionStatusColor = "#10B981"; // Green success
            }
            else
            {
                ConnectionStatusText = Translations.Status_BexioDisconnected;
                ConnectionStatusColor = "#EF4444"; // Red error
            }
        }
        catch
        {
            ConnectionStatusText = Translations.Status_BexioDisconnected;
            ConnectionStatusColor = "#EF4444"; // Red error
        }
    }

    public void AppendLog(string message)
    {
        LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
    }

    public async Task LoadExcelFileAsync(string? filePath = null)
    {
        if (filePath == null)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = Translations.Import_SelectFileTitle
            };
            if (openFileDialog.ShowDialog() == true)
            {
                filePath = openFileDialog.FileName;
            }
        }

        if (string.IsNullOrEmpty(filePath)) return;

        SelectedFilePath = filePath;
        AppendLog(string.Format("Reading Excel file: {0}", Path.GetFileName(filePath)));
        IsLoading = true;

        try
        {
            var options = BuildMappingOptions();
            var parser = new ClosedXmlExcelParser(Options.Create(options));
            
            // Parse on background thread to keep UI responsive and allow spinner animation
            _loadedOrder = await Task.Run(() => parser.ParseOrderForm(filePath));
            
            // Populate file info
            var fileInfo = new FileInfo(filePath);
            FileSizeText = $"{fileInfo.Length / 1024.0:F1} KB";
            SelectedFileName = Path.GetFileName(filePath);
            HasLoadedFile = true;
            
            // Populate GUI bindings
            CompanyName = _loadedOrder.Customer.CompanyName;
            BuyerName = _loadedOrder.Customer.BuyerName;
            Email = _loadedOrder.Customer.Email;
            Address = $"{_loadedOrder.Customer.Street}, {_loadedOrder.Customer.ZipCode} {_loadedOrder.Customer.City}";
            DeliveryDate = _loadedOrder.DeliveryDate?.ToString("dd.MM.yyyy") ?? "Nicht definiert";
            PaymentTerms = _loadedOrder.PaymentTerms;

            OrderPositions.Clear();
            foreach (var pos in _loadedOrder.Positions)
            {
                OrderPositions.Add(pos);
            }

            UpdateTotalsSummary();
            ImportCommand.RaiseCanExecuteChanged();
            AppendLog($"Successfully read: {_loadedOrder.Positions.Count} positions found.");
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] Error reading Excel file: {ex.Message}");
            _loadedOrder = null;
            HasLoadedFile = false;
            ImportCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void UpdateTotalsSummary()
    {
        if (_loadedOrder == null)
        {
            TotalsSummary = string.Empty;
            TotalQuantity = 0;
            TotalGrossAmount = 0;
            DiscountPercentVal = 0;
            DiscountAmount = 0;
            TotalNetAmount = 0;
            return;
        }

        // Recalculate based on currently editted positions in the grid
        TotalQuantity = OrderPositions.Sum(p => p.Quantity);
        TotalGrossAmount = OrderPositions.Sum(p => p.TotalPrice);
        DiscountPercentVal = _loadedOrder.DiscountPercent;
        DiscountAmount = TotalGrossAmount * (DiscountPercentVal / 100m);
        TotalNetAmount = TotalGrossAmount - DiscountAmount;

        TotalsSummary = $"{Translations.Import_SummaryQuantity}: {TotalQuantity} | {Translations.Import_SummaryGross}: {TotalGrossAmount:F2} CHF | {Translations.Import_SummaryDiscount}: {DiscountPercentVal}% | {Translations.Import_SummaryNet}: {TotalNetAmount:F2} CHF";
    }

    private void ClearLoadedFile()
    {
        _loadedOrder = null;
        SelectedFilePath = null;
        SelectedFileName = string.Empty;
        FileSizeText = string.Empty;
        HasLoadedFile = false;
        
        CompanyName = string.Empty;
        BuyerName = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        DeliveryDate = string.Empty;
        PaymentTerms = string.Empty;
        
        OrderPositions.Clear();
        UpdateTotalsSummary();
        ImportCommand.RaiseCanExecuteChanged();
        AppendLog("File upload was deleted by user.");
    }

    private void ClearLoadedFileAfterSuccess()
    {
        _loadedOrder = null;
        SelectedFilePath = null;
        SelectedFileName = string.Empty;
        FileSizeText = string.Empty;
        HasLoadedFile = false;
        
        CompanyName = string.Empty;
        BuyerName = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        DeliveryDate = string.Empty;
        PaymentTerms = string.Empty;
        
        OrderPositions.Clear();
        UpdateTotalsSummary();
        ImportCommand.RaiseCanExecuteChanged();
        AppendLog("Import completed successfully. File selection reset.");
    }

    private async Task ImportToBexioAsync()
    {
        if (_loadedOrder == null) return;

        IsImporting = true;
        IsImportingActive = true;
        LogText = string.Empty;
        ProgressPercentage = 0;
        AppendLog("Starting import process...");

        try
        {
            // Sync values from DataGrid back to Order Positions
            _loadedOrder.Positions = OrderPositions.ToList();

            using var httpClient = new HttpClient();
            var bexioClient = new BexioApiClient(httpClient, BexioToken, DefaultAccountId, DefaultTaxId);
            var useCase = new ImportOrderUseCase(new InMemoryExcelParser(_loadedOrder), bexioClient);

            bool success = await useCase.ExecuteAsync(
                filePath: SelectedFilePath!,
                showPreviewCallback: order => { }, // Already shown in UI
                confirmUploadCallback: ConfirmUploadAsync,
                confirmCustomerCreationCallback: ConfirmCustomerCreationAsync,
                logInfoCallback: message =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        AppendLog(message);
                        
                        // Parse progress from log message if contains progress info
                        if (message.Contains("Positionen hochgeladen:"))
                        {
                            var parts = message.Split(':').Last().Trim().Split('/');
                            if (parts.Length == 2 && double.TryParse(parts[0], out double uploaded) && double.TryParse(parts[1], out double total))
                            {
                                ProgressPercentage = (uploaded / total) * 100;
                            }
                        }
                    });
                }
            );
            
            if (success)
            {
                ProgressPercentage = 100;
                App.Current.Dispatcher.Invoke(() =>
                {
                    ClearLoadedFileAfterSuccess();
                });
            }
            else
            {
                ProgressPercentage = 0;
                AppendLog("Import cancelled. File remains loaded.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] Error during import: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
            IsImportingActive = false;
        }
    }

    private async Task<bool> ConfirmUploadAsync()
    {
        return await App.Current.Dispatcher.InvokeAsync(() =>
        {
            IsImportingActive = false;
            try
            {
                return Views.CustomDialog.ShowConfirm(Translations.Import_ConfirmMessage, Translations.Import_ConfirmTitle);
            }
            finally
            {
                // Only restore if the import hasn't been terminated/disposed
                if (IsImporting) IsImportingActive = true;
            }
        });
    }

    private async Task<bool> ConfirmCustomerCreationAsync(Customer customer)
    {
        return await App.Current.Dispatcher.InvokeAsync(() =>
        {
            IsImportingActive = false;
            try
            {
                var dialog = new CustomerConfirmWindow(customer);
                dialog.Owner = App.Current.MainWindow;
                return dialog.ShowDialog() == true;
            }
            finally
            {
                if (IsImporting) IsImportingActive = true;
            }
        });
    }

    private async Task CheckForUpdatesAsync()
    {
        var info = await _updateService.CheckForUpdatesAsync();
        if (info != null)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _updateDownloadUrl = info.DownloadUrl;
                UpdateVersion = info.LatestVersion;
                IsUpdateAvailable = true;
                InstallUpdateCommand.RaiseCanExecuteChanged();
            });
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl)) return;

        IsDownloadingUpdate = true;
        InstallUpdateCommand.RaiseCanExecuteChanged();
        UpdateStatusText = string.Format(Translations.Update_Downloading, 0);

        try
        {
            await Task.Run(async () =>
            {
                await _updateService.DownloadAndInstallUpdateAsync(_updateDownloadUrl, progress =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateProgress = progress;
                        UpdateStatusText = string.Format(Translations.Update_Downloading, progress);
                    });
                });
            });
        }
        catch (Exception ex)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsDownloadingUpdate = false;
                InstallUpdateCommand.RaiseCanExecuteChanged();
                UpdateStatusText = string.Format(Translations.Update_Error, ex.Message);
            });
        }
    }

    // Settings persistence
    private void EnsureAppSettingsFile()
    {
        string? dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(_configFilePath))
        {
            // Check if there is an appsettings.json in the application directory to use as a template
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(templatePath))
            {
                try
                {
                    File.Copy(templatePath, _configFilePath, true);
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"[Warning] Could not copy appsettings.json template: {ex.Message}");
                }
            }

            var defaultSettings = new
            {
                Bexio = new { ApiToken = "bexio_api_token_here", DefaultAccountId = 3200, DefaultTaxId = 1, Language = "de" },
                ActiveProfileName = "Default",
                Profiles = new[]
                {
                    new
                    {
                        Name = "Default",
                        ExcelMapping = new
                        {
                            WorksheetIndex = 1,
                            Header = new
                            {
                                CompanyNameCell = "B4",
                                StreetCell = "B5",
                                ZipCityCell = "B6",
                                BuyerEmailCell = "E5",
                                BuyerNameCell = "E4",
                                DeliveryDateCell = "T7",
                                PaymentTermsCell = "A9",
                                DiscountCell = "V12"
                            },
                            SizeMatrix = new
                            {
                                StartRow = 10,
                                EndRow = 17,
                                CategoryColumn = 4,
                                StartSizeColumn = 5,
                                EndSizeColumn = 18
                            },
                            Data = new
                            {
                                StartRow = 18,
                                ArticleNumberColumn = 1,
                                ArticleNameColumn = 2,
                                ColorColumn = 3,
                                CategoryColumn = 4,
                                StartQtyColumn = 5,
                                EndQtyColumn = 18,
                                UnitPriceColumn = 20
                            }
                        }
                    }
                }
            };
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private void LoadSettings()
    {
        try
        {
            EnsureAppSettingsFile();

            using var doc = JsonDocument.Parse(File.ReadAllText(_configFilePath));
            var root = doc.RootElement;

            if (root.TryGetProperty("Bexio", out var bexio))
            {
                var encryptedToken = bexio.GetProperty("ApiToken").GetString() ?? "";
                BexioToken = Helpers.EncryptionHelper.Decrypt(encryptedToken);
                if (string.IsNullOrEmpty(BexioToken) && !string.IsNullOrEmpty(encryptedToken) && encryptedToken != "bexio_api_token_here")
                {
                    BexioToken = encryptedToken;
                }
                
                DefaultAccountId = bexio.GetProperty("DefaultAccountId").GetInt32();
                DefaultTaxId = bexio.GetProperty("DefaultTaxId").GetInt32();
                if (bexio.TryGetProperty("Language", out var langProp))
                {
                    SelectedLanguage = langProp.GetString() ?? "de";
                }
                else
                {
                    SelectedLanguage = "de";
                }
                _initialLanguage = SelectedLanguage;
                ApplyLanguage(SelectedLanguage);
            }

            Profiles.Clear();
            string activeProfileName = "Default";
            if (root.TryGetProperty("ActiveProfileName", out var activeNameProp))
            {
                activeProfileName = activeNameProp.GetString() ?? "Default";
            }

            if (root.TryGetProperty("Profiles", out var profilesEl) && profilesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in profilesEl.EnumerateArray())
                {
                    var profile = new Models.MappingProfile
                    {
                        Name = el.GetProperty("Name").GetString() ?? "Default",
                        Mapping = DeserializeMapping(el.GetProperty("ExcelMapping"))
                    };
                    Profiles.Add(profile);
                }
            }

            if (Profiles.Count == 0)
            {
                var defaultMapping = new ExcelMappingOptions();
                if (root.TryGetProperty("ExcelMapping", out var oldMapping))
                {
                    defaultMapping = DeserializeMapping(oldMapping);
                }
                Profiles.Add(new Models.MappingProfile { Name = "Default", Mapping = defaultMapping });
            }

            var active = Profiles.FirstOrDefault(p => p.Name.Equals(activeProfileName, StringComparison.OrdinalIgnoreCase)) ?? Profiles[0];
            _activeProfile = active;
            SelectedProfile = active;
            OnPropertyChanged(nameof(ActiveProfile));
        }
        catch (Exception ex)
        {
            Views.CustomDialog.ShowError($"{Translations.Settings_ErrorLoad}: {ex.Message}", Translations.Settings_ErrorTitle);
        }

        IsModified = false;
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    private ExcelMappingOptions DeserializeMapping(JsonElement el)
    {
        var options = new ExcelMappingOptions();
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null)
            return options;

        if (el.TryGetProperty("WorksheetIndex", out var wsIndex)) options.WorksheetIndex = wsIndex.GetInt32();
        
        if (el.TryGetProperty("Header", out var header))
        {
            if (header.TryGetProperty("CompanyNameCell", out var cell)) options.Header.CompanyNameCell = cell.GetString() ?? "B4";
            if (header.TryGetProperty("StreetCell", out var cell2)) options.Header.StreetCell = cell2.GetString() ?? "B5";
            if (header.TryGetProperty("ZipCityCell", out var cell3)) options.Header.ZipCityCell = cell3.GetString() ?? "B6";
            if (header.TryGetProperty("BuyerEmailCell", out var cell4)) options.Header.BuyerEmailCell = cell4.GetString() ?? "E5";
            if (header.TryGetProperty("BuyerNameCell", out var cell5)) options.Header.BuyerNameCell = cell5.GetString() ?? "E4";
            if (header.TryGetProperty("DeliveryDateCell", out var cell6)) options.Header.DeliveryDateCell = cell6.GetString() ?? "T7";
            if (header.TryGetProperty("PaymentTermsCell", out var cell7)) options.Header.PaymentTermsCell = cell7.GetString() ?? "A9";
            if (header.TryGetProperty("DiscountCell", out var cell8)) options.Header.DiscountCell = cell8.GetString() ?? "V12";
        }

        if (el.TryGetProperty("SizeMatrix", out var matrix))
        {
            if (matrix.TryGetProperty("StartRow", out var r)) options.SizeMatrix.StartRow = r.GetInt32();
            if (matrix.TryGetProperty("EndRow", out var r2)) options.SizeMatrix.EndRow = r2.GetInt32();
            if (matrix.TryGetProperty("CategoryColumn", out var c)) options.SizeMatrix.CategoryColumn = c.GetInt32();
            if (matrix.TryGetProperty("StartSizeColumn", out var c2)) options.SizeMatrix.StartSizeColumn = c2.GetInt32();
            if (matrix.TryGetProperty("EndSizeColumn", out var c3)) options.SizeMatrix.EndSizeColumn = c3.GetInt32();
        }

        if (el.TryGetProperty("Data", out var data))
        {
            if (data.TryGetProperty("StartRow", out var r)) options.Data.StartRow = r.GetInt32();
            if (data.TryGetProperty("ArticleNumberColumn", out var c)) options.Data.ArticleNumberColumn = c.GetInt32();
            if (data.TryGetProperty("ArticleNameColumn", out var c2)) options.Data.ArticleNameColumn = c2.GetInt32();
            if (data.TryGetProperty("ColorColumn", out var c3)) options.Data.ColorColumn = c3.GetInt32();
            if (data.TryGetProperty("CategoryColumn", out var c4)) options.Data.CategoryColumn = c4.GetInt32();
            if (data.TryGetProperty("StartQtyColumn", out var c5)) options.Data.StartQtyColumn = c5.GetInt32();
            if (data.TryGetProperty("EndQtyColumn", out var c6)) options.Data.EndQtyColumn = c6.GetInt32();
            if (data.TryGetProperty("UnitPriceColumn", out var c7)) options.Data.UnitPriceColumn = c7.GetInt32();
        }

        return options;
    }

    private void SaveSettings()
    {
        try
        {
            if (SelectedProfile != null)
            {
                CopyVmToProfile(SelectedProfile);
            }

            string encryptedToken = Helpers.EncryptionHelper.Encrypt(BexioToken);

            var settingsObj = new
            {
                Bexio = new { ApiToken = encryptedToken, DefaultAccountId = DefaultAccountId, DefaultTaxId = DefaultTaxId, Language = SelectedLanguage },
                ActiveProfileName = ActiveProfile?.Name ?? "Default",
                Profiles = Profiles.Select(p => new
                {
                    Name = p.Name,
                    ExcelMapping = new
                    {
                        WorksheetIndex = p.Mapping.WorksheetIndex,
                        Header = new
                        {
                            CompanyNameCell = p.Mapping.Header.CompanyNameCell,
                            StreetCell = p.Mapping.Header.StreetCell,
                            ZipCityCell = p.Mapping.Header.ZipCityCell,
                            BuyerEmailCell = p.Mapping.Header.BuyerEmailCell,
                            BuyerNameCell = p.Mapping.Header.BuyerNameCell,
                            DeliveryDateCell = p.Mapping.Header.DeliveryDateCell,
                            PaymentTermsCell = p.Mapping.Header.PaymentTermsCell,
                            DiscountCell = p.Mapping.Header.DiscountCell
                        },
                        SizeMatrix = new
                        {
                            StartRow = p.Mapping.SizeMatrix.StartRow,
                            EndRow = p.Mapping.SizeMatrix.EndRow,
                            CategoryColumn = p.Mapping.SizeMatrix.CategoryColumn,
                            StartSizeColumn = p.Mapping.SizeMatrix.StartSizeColumn,
                            EndSizeColumn = p.Mapping.SizeMatrix.EndSizeColumn
                        },
                        Data = new
                        {
                            StartRow = p.Mapping.Data.StartRow,
                            ArticleNumberColumn = p.Mapping.Data.ArticleNumberColumn,
                            ArticleNameColumn = p.Mapping.Data.ArticleNameColumn,
                            ColorColumn = p.Mapping.Data.ColorColumn,
                            CategoryColumn = p.Mapping.Data.CategoryColumn,
                            StartQtyColumn = p.Mapping.Data.StartQtyColumn,
                            EndQtyColumn = p.Mapping.Data.EndQtyColumn,
                            UnitPriceColumn = p.Mapping.Data.UnitPriceColumn
                        }
                    }
                }).ToArray()
            };

            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true }));
            
            _ = CheckBexioConnectionAsync();

            if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }

            ApplyLanguage(SelectedLanguage);
            bool languageChanged = SelectedLanguage != _initialLanguage;

            if (languageChanged)
            {
                bool reload = Views.CustomDialog.ShowConfirm(
                    Translations.Settings_ReloadPromptMessage,
                    Translations.Settings_ReloadPromptTitle);

                if (reload)
                {
                    App.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var newWindow = new Views.MainWindow();
                        newWindow.Show();
                        App.Current.MainWindow.Close();
                        App.Current.MainWindow = newWindow;
                    }));
                }
                _initialLanguage = SelectedLanguage;
            }
            else
            {
                Views.CustomDialog.ShowInfo(Translations.Dialog_SettingsSaved);
            }
            
            AppendLog("Settings saved successfully and active Excel file reloaded.");
            IsModified = false;
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            Views.CustomDialog.ShowError($"{Translations.Settings_ErrorSave}: {ex.Message}", Translations.Settings_ErrorTitle);
        }
    }

    private void CopyVmToProfile(Models.MappingProfile profile)
    {
        profile.Mapping.Header.CompanyNameCell = CompanyNameCell;
        profile.Mapping.Header.StreetCell = StreetCell;
        profile.Mapping.Header.ZipCityCell = ZipCityCell;
        profile.Mapping.Header.BuyerEmailCell = BuyerEmailCell;
        profile.Mapping.Header.BuyerNameCell = BuyerNameCell;
        profile.Mapping.Header.DeliveryDateCell = DeliveryDateCell;
        profile.Mapping.Header.PaymentTermsCell = PaymentTermsCell;
        profile.Mapping.Header.DiscountCell = DiscountCell;

        profile.Mapping.SizeMatrix.StartRow = MatrixStartRow;
        profile.Mapping.SizeMatrix.EndRow = MatrixEndRow;
        profile.Mapping.SizeMatrix.CategoryColumn = MatrixCategoryCol;
        profile.Mapping.SizeMatrix.StartSizeColumn = MatrixStartSizeCol;
        profile.Mapping.SizeMatrix.EndSizeColumn = MatrixEndSizeCol;

        profile.Mapping.Data.StartRow = DataStartRow;
        profile.Mapping.Data.ArticleNumberColumn = ColArtNum;
        profile.Mapping.Data.ArticleNameColumn = ColArtName;
        profile.Mapping.Data.ColorColumn = ColColor;
        profile.Mapping.Data.CategoryColumn = ColSizeCategory;
        profile.Mapping.Data.StartQtyColumn = ColStartQty;
        profile.Mapping.Data.EndQtyColumn = ColEndQty;
        profile.Mapping.Data.UnitPriceColumn = ColUnitPrice;
    }

    private void CopyProfileToVm(Models.MappingProfile profile)
    {
        CompanyNameCell = profile.Mapping.Header.CompanyNameCell;
        StreetCell = profile.Mapping.Header.StreetCell;
        ZipCityCell = profile.Mapping.Header.ZipCityCell;
        BuyerEmailCell = profile.Mapping.Header.BuyerEmailCell;
        BuyerNameCell = profile.Mapping.Header.BuyerNameCell;
        DeliveryDateCell = profile.Mapping.Header.DeliveryDateCell;
        PaymentTermsCell = profile.Mapping.Header.PaymentTermsCell;
        DiscountCell = profile.Mapping.Header.DiscountCell;

        MatrixStartRow = profile.Mapping.SizeMatrix.StartRow;
        MatrixEndRow = profile.Mapping.SizeMatrix.EndRow;
        MatrixCategoryCol = profile.Mapping.SizeMatrix.CategoryColumn;
        MatrixStartSizeCol = profile.Mapping.SizeMatrix.StartSizeColumn;
        MatrixEndSizeCol = profile.Mapping.SizeMatrix.EndSizeColumn;

        DataStartRow = profile.Mapping.Data.StartRow;
        ColArtNum = profile.Mapping.Data.ArticleNumberColumn;
        ColArtName = profile.Mapping.Data.ArticleNameColumn;
        ColColor = profile.Mapping.Data.ColorColumn;
        ColSizeCategory = profile.Mapping.Data.CategoryColumn;
        ColStartQty = profile.Mapping.Data.StartQtyColumn;
        ColEndQty = profile.Mapping.Data.EndQtyColumn;
        ColUnitPrice = profile.Mapping.Data.UnitPriceColumn;
    }

    private void CreateProfile()
    {
        var dialog = new Views.ProfileCreateDialog(isClone: false);
        dialog.Owner = App.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            string name = dialog.ProfileName;
            if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Views.CustomDialog.ShowError(Translations.Dialog_ProfileNameExists, Translations.Dialog_ErrorTitle);
                return;
            }

            var newProfile = new Models.MappingProfile
            {
                Name = name,
                Mapping = new ExcelMappingOptions()
            };
            Profiles.Add(newProfile);
            SelectedProfile = newProfile;
            SetModified();

            // Directly open update window
            EditProfile(newProfile);
        }
    }

    private void CloneProfile(Models.MappingProfile profile)
    {
        if (profile == null) return;
        var dialog = new Views.ProfileCreateDialog(isClone: true);
        dialog.Owner = App.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            string name = dialog.ProfileName;
            if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Views.CustomDialog.ShowError(Translations.Dialog_ProfileNameExists, Translations.Dialog_ErrorTitle);
                return;
            }

            var newProfile = new Models.MappingProfile
            {
                Name = name,
                Mapping = CloneMapping(profile.Mapping)
            };
            Profiles.Add(newProfile);
            SelectedProfile = newProfile;
            SetModified();
        }
    }

    private void EditProfile(Models.MappingProfile profile)
    {
        if (profile == null) return;
        var editWindow = new Views.ProfileEditWindow(profile);
        editWindow.Owner = App.Current.MainWindow;
        if (editWindow.ShowDialog() == true)
        {
            if (profile == SelectedProfile)
            {
                CopyProfileToVm(profile);
            }
            if (profile == ActiveProfile)
            {
                if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
                {
                    _ = LoadExcelFileAsync(SelectedFilePath);
                }
            }
            SetModified();
        }
    }

    private void DeleteProfile(Models.MappingProfile profile)
    {
        if (profile == null || profile.Name == "Default" || Profiles.Count <= 1) return;

        string message = string.Format(Translations.Confirm_DeleteProfileMessage, profile.Name);
        bool confirmed = Views.CustomDialog.ShowConfirm(message, Translations.Confirm_DeleteProfileTitle);
        if (!confirmed) return;

        Profiles.Remove(profile);

        if (SelectedProfile == profile)
        {
            SelectedProfile = Profiles[0];
        }
        if (ActiveProfile == profile)
        {
            ActiveProfile = Profiles[0];
        }
        SetModified();
    }

    private void SetActiveProfile(Models.MappingProfile profile)
    {
        if (profile != null)
        {
            ActiveProfile = profile;
            OnPropertyChanged(nameof(ActiveProfile));
            AppendLog($"Active profile set to: {ActiveProfile.Name}");
            SetModified();
        }
    }

    private void ExportProfiles()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json",
                FileName = "bexio_mapping_profiles.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var exportList = Profiles.Select(p => new
                {
                    Name = p.Name,
                    ExcelMapping = new
                    {
                        WorksheetIndex = p.Mapping.WorksheetIndex,
                        Header = new
                        {
                            CompanyNameCell = p.Mapping.Header.CompanyNameCell,
                            StreetCell = p.Mapping.Header.StreetCell,
                            ZipCityCell = p.Mapping.Header.ZipCityCell,
                            BuyerEmailCell = p.Mapping.Header.BuyerEmailCell,
                            BuyerNameCell = p.Mapping.Header.BuyerNameCell,
                            DeliveryDateCell = p.Mapping.Header.DeliveryDateCell,
                            PaymentTermsCell = p.Mapping.Header.PaymentTermsCell,
                            DiscountCell = p.Mapping.Header.DiscountCell
                        },
                        SizeMatrix = new
                        {
                            StartRow = p.Mapping.SizeMatrix.StartRow,
                            EndRow = p.Mapping.SizeMatrix.EndRow,
                            CategoryColumn = p.Mapping.SizeMatrix.CategoryColumn,
                            StartSizeColumn = p.Mapping.SizeMatrix.StartSizeColumn,
                            EndSizeColumn = p.Mapping.SizeMatrix.EndSizeColumn
                        },
                        Data = new
                        {
                            StartRow = p.Mapping.Data.StartRow,
                            ArticleNumberColumn = p.Mapping.Data.ArticleNumberColumn,
                            ArticleNameColumn = p.Mapping.Data.ArticleNameColumn,
                            ColorColumn = p.Mapping.Data.ColorColumn,
                            CategoryColumn = p.Mapping.Data.CategoryColumn,
                            StartQtyColumn = p.Mapping.Data.StartQtyColumn,
                            EndQtyColumn = p.Mapping.Data.EndQtyColumn,
                            UnitPriceColumn = p.Mapping.Data.UnitPriceColumn
                        }
                    }
                }).ToList();

                string json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
                AppendLog($"Profiles exported successfully to: {dialog.FileName}");
                Views.CustomDialog.ShowInfo(Translations.Dialog_ExportSuccess);
            }
        }
        catch (Exception ex)
        {
            Views.CustomDialog.ShowError($"{Translations.Settings_ErrorSave}: {ex.Message}", Translations.Settings_ErrorTitle);
        }
    }

    private void ImportProfiles()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(dialog.FileName));
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    Views.CustomDialog.ShowError(Translations.Dialog_ImportInvalidFormat, Translations.Dialog_ErrorTitle);
                    return;
                }

                bool importedAny = false;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (!el.TryGetProperty("Name", out var nameProp)) continue;
                    string name = nameProp.GetString() ?? "";
                    if (string.IsNullOrEmpty(name)) continue;

                    ExcelMappingOptions mapping = DeserializeMapping(el.GetProperty("ExcelMapping"));

                    var existing = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Mapping = mapping;
                        if (existing == SelectedProfile)
                        {
                            CopyProfileToVm(existing);
                        }
                        if (existing == ActiveProfile)
                        {
                            if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
                            {
                                _ = LoadExcelFileAsync(SelectedFilePath);
                            }
                        }
                    }
                    else
                    {
                        Profiles.Add(new Models.MappingProfile { Name = name, Mapping = mapping });
                    }
                    importedAny = true;
                }

                if (importedAny)
                {
                    SetModified();
                    AppendLog($"Profiles imported successfully from: {dialog.FileName}");
                    Views.CustomDialog.ShowInfo(Translations.Dialog_ImportSuccess);
                }
            }
        }
        catch (Exception ex)
        {
            Views.CustomDialog.ShowError($"{Translations.Settings_ErrorLoad}: {ex.Message}", Translations.Settings_ErrorTitle);
        }
    }

    private ExcelMappingOptions CloneMapping(ExcelMappingOptions source)
    {
        return new ExcelMappingOptions
        {
            WorksheetIndex = source.WorksheetIndex,
            Header = new HeaderMapping
            {
                CompanyNameCell = source.Header.CompanyNameCell,
                StreetCell = source.Header.StreetCell,
                ZipCityCell = source.Header.ZipCityCell,
                BuyerEmailCell = source.Header.BuyerEmailCell,
                BuyerNameCell = source.Header.BuyerNameCell,
                DeliveryDateCell = source.Header.DeliveryDateCell,
                PaymentTermsCell = source.Header.PaymentTermsCell,
                DiscountCell = source.Header.DiscountCell
            },
            SizeMatrix = new SizeMatrixMapping
            {
                StartRow = source.SizeMatrix.StartRow,
                EndRow = source.SizeMatrix.EndRow,
                CategoryColumn = source.SizeMatrix.CategoryColumn,
                StartSizeColumn = source.SizeMatrix.StartSizeColumn,
                EndSizeColumn = source.SizeMatrix.EndSizeColumn
            },
            Data = new DataMapping
            {
                StartRow = source.Data.StartRow,
                ArticleNumberColumn = source.Data.ArticleNumberColumn,
                ArticleNameColumn = source.Data.ArticleNameColumn,
                ColorColumn = source.Data.ColorColumn,
                CategoryColumn = source.Data.CategoryColumn,
                StartQtyColumn = source.Data.StartQtyColumn,
                EndQtyColumn = source.Data.EndQtyColumn,
                UnitPriceColumn = source.Data.UnitPriceColumn
            }
        };
    }

    private ExcelMappingOptions BuildMappingOptions()
    {
        return ActiveProfile != null ? ActiveProfile.Mapping : new ExcelMappingOptions();
    }

    private void ApplyLanguage(string language)
    {
        var culture = new System.Globalization.CultureInfo(language == "en" ? "en-US" : "de-CH");
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}

public class InMemoryExcelParser : IExcelParser
{
    private readonly Order _order;
    public InMemoryExcelParser(Order order) => _order = order;
    public Order ParseOrderForm(string filePath) => _order;
}
