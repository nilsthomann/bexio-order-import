using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Resources;
using BexioOrderImport.Wpf.Services;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
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

    private readonly IUpdateService _updateService;
    private readonly IBexioClientFactory _bexioClientFactory;
    private readonly StringBuilder _logBuilder = new();
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

    // Dialog and Window providers (can be overridden in tests to avoid blocking UI)
    public Func<bool, string?> ProfileCreateDialogProvider { get; set; } = isClone =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            return App.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Views.ProfileCreateDialog(isClone);
                dialog.Owner = App.Current?.MainWindow;
                return dialog.ShowDialog() == true ? dialog.ProfileName : null;
            });
        }
        var dialog = new Views.ProfileCreateDialog(isClone);
        dialog.Owner = App.Current?.MainWindow;
        return dialog.ShowDialog() == true ? dialog.ProfileName : null;
    };

    public Func<Models.MappingProfile, bool> ProfileEditDialogProvider { get; set; } = profile =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            return App.Current.Dispatcher.Invoke(() =>
            {
                var editWindow = new Views.ProfileEditWindow(profile);
                editWindow.Owner = App.Current?.MainWindow;
                return editWindow.ShowDialog() == true;
            });
        }
        var editWindow = new Views.ProfileEditWindow(profile);
        editWindow.Owner = App.Current?.MainWindow;
        return editWindow.ShowDialog() == true;
    };

    public Func<string, string, string?> OpenFileDialogProvider { get; set; } = (filter, defaultExt) =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            return App.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = filter,
                    DefaultExt = defaultExt
                };
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            });
        }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExt
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    };

    public Func<string, string, string, string?> SaveFileDialogProvider { get; set; } = (filter, defaultExt, defaultFileName) =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            return App.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = filter,
                    DefaultExt = defaultExt,
                    FileName = defaultFileName
                };
                return dialog.ShowDialog() == true ? dialog.FileName : null;
            });
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = defaultFileName
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    };

    public Func<string, string, bool> ConfirmDialogProvider { get; set; } = (message, title) =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            return App.Current.Dispatcher.Invoke(() => Views.CustomDialog.ShowConfirm(message, title));
        }
        return Views.CustomDialog.ShowConfirm(message, title);
    };

    public Func<Customer, bool> CustomerConfirmDialogProvider { get; set; } = customer =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            return App.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new Views.CustomerConfirmWindow(customer);
                dialog.Owner = App.Current?.MainWindow;
                return dialog.ShowDialog() == true;
            });
        }
        var dialog = new Views.CustomerConfirmWindow(customer);
        dialog.Owner = App.Current?.MainWindow;
        return dialog.ShowDialog() == true;
    };

    public Action<string, string> ErrorDialogProvider { get; set; } = (message, title) =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            App.Current.Dispatcher.Invoke(() => Views.CustomDialog.ShowError(message, title));
            return;
        }
        Views.CustomDialog.ShowError(message, title);
    };

    public Action<string> InfoDialogProvider { get; set; } = message =>
    {
        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            App.Current.Dispatcher.Invoke(() => Views.CustomDialog.ShowInfo(message));
            return;
        }
        Views.CustomDialog.ShowInfo(message);
    };

    public Func<string, Task<bool>>? BexioConnectionCheckTestHook { get; set; }

    public void InvokeOnUi(Action action)
    {
        if (App.Current?.Dispatcher != null)
        {
            App.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void InvokeOnUiAsync(Action action)
    {
        if (App.Current?.Dispatcher != null)
        {
            App.Current.Dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public MainViewModel(IUpdateService updateService, IBexioClientFactory bexioClientFactory, string? configFilePath = null)
    {
        _updateService = updateService;
        _bexioClientFactory = bexioClientFactory;

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
        if (configFilePath != null)
        {
            _configFilePath = configFilePath;
        }
        else
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
            _configFilePath = Path.Combine(appDataFolder, "appsettings.json");
        }
        
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

    public void AppendLog(string message)
    {
        _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogText = _logBuilder.ToString();
    }
}
