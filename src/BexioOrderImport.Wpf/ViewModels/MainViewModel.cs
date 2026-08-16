using System;
using System.IO;
using System.Text;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Wpf.Resources;
using BexioOrderImport.Wpf.Services;
using System.Reflection;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    internal string _configFilePath;
    internal Order? _loadedOrder;
    private string _connectionStatusText = Translations.Status_BexioDisconnected;
    private string _connectionStatusColor = ConnectionColorDisconnected;
    private double _progressPercentage;
    private string _remainingTimeText = string.Empty;
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
    private readonly IExcelParserFactory _excelParserFactory;
    private readonly IDialogService _dialogService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IEncryptionService _encryptionService;
    private readonly IProfileManagerService _profileManagerService;
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
    private decimal _totalNetAmount;

    // Excel Order header properties (bound to UI fields)
    private string _companyName = string.Empty;
    private string _buyerName = string.Empty;
    private string _email = string.Empty;
    private string _address = string.Empty;
    private string _orderId = string.Empty;
    private string _customerId = string.Empty;
    private string _paymentTerms = string.Empty;

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

    public decimal TotalNetAmount
    {
        get => _totalNetAmount;
        set => SetProperty(ref _totalNetAmount, value);
    }

    // Settings fields (bound to Bexio API connection)
    private string _bexioToken = string.Empty;
    private int? _accountId = null;
    private int? _taxId = null;

    internal void InvokeOnUi(Action action)
    {
        _dispatcherService.Invoke(action);
    }

    internal void InvokeOnUiAsync(Action action)
    {
        _dispatcherService.BeginInvoke(action);
    }

    public MainViewModel(
        IUpdateService updateService,
        IBexioClientFactory bexioClientFactory,
        IDialogService dialogService,
        IDispatcherService dispatcherService,
        IEncryptionService encryptionService,
        IExcelParserFactory excelParserFactory,
        string? configFilePath = null,
        IProfileManagerService? profileManagerService = null)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _bexioClientFactory = bexioClientFactory ?? throw new ArgumentNullException(nameof(bexioClientFactory));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _excelParserFactory = excelParserFactory ?? throw new ArgumentNullException(nameof(excelParserFactory));
        _profileManagerService = profileManagerService ?? new ProfileManagerService(_dialogService);

        // Commands
        LoadFileCommand = new RelayCommand(async () => await LoadExcelFileAsync());
        ClearFileCommand = new RelayCommand(ClearLoadedFile);
        ImportCommand = new RelayCommand(async () => await ImportToBexioAsync(), () => _loadedOrder != null && !_isImporting);
        SaveSettingsCommand = new RelayCommand(SaveSettings, () => IsModified);
        CreateProfileCommand = new RelayCommand(CreateProfile);
        EditProfileCommand = new RelayCommand<Models.MappingProfile>(EditProfile);
        CloneProfileCommand = new RelayCommand<Models.MappingProfile>(CloneProfile);
        SetActiveProfileCommand = new RelayCommand<Models.MappingProfile>(SetActiveProfile);
        DeleteProfileCommand = new RelayCommand<Models.MappingProfile>(DeleteProfile, p => p != null && Profiles.Count > 1);
        ExportProfilesCommand = new RelayCommand(ExportProfiles);
        ImportProfilesCommand = new RelayCommand(ImportProfiles);
        InstallUpdateCommand = new RelayCommand(async () => await InstallUpdateAsync(), () => !string.IsNullOrEmpty(_updateDownloadUrl) && !_isDownloadingUpdate);
        CloseImportSuccessCommand = new RelayCommand(CloseImportSuccess);

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
    public RelayCommand CloseImportSuccessCommand { get; }

    private bool _isImportSuccess;
    private string _importSuccessTitle = string.Empty;
    private string _importSuccessMessage = string.Empty;
    private string _importDurationText = string.Empty;

    public bool IsImportSuccess
    {
        get => _isImportSuccess;
        set => SetProperty(ref _isImportSuccess, value);
    }

    public string ImportSuccessTitle
    {
        get => _importSuccessTitle;
        set => SetProperty(ref _importSuccessTitle, value);
    }

    public string ImportSuccessMessage
    {
        get => _importSuccessMessage;
        set => SetProperty(ref _importSuccessMessage, value);
    }

    public string ImportDurationText
    {
        get => _importDurationText;
        set => SetProperty(ref _importDurationText, value);
    }

    private void CloseImportSuccess()
    {
        IsImportingActive = false;
        IsImportSuccess = false;
        ClearLoadedFileInternal("Import completed successfully. File selection reset.");
    }

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

    private bool _isConnectionSuccessful;
    public bool IsConnectionSuccessful
    {
        get => _isConnectionSuccessful;
        set => SetProperty(ref _isConnectionSuccessful, value);
    }

    public ObservableCollection<BexioAccount> AccountsList { get; } = new();
    public ObservableCollection<BexioTax> TaxesList { get; } = new();

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string RemainingTimeText
    {
        get => _remainingTimeText;
        set => SetProperty(ref _remainingTimeText, value);
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

    public string OrderId
    {
        get => _orderId;
        set => SetProperty(ref _orderId, value);
    }

    public string CustomerId
    {
        get => _customerId;
        set => SetProperty(ref _customerId, value);
    }

    public string PaymentTerms
    {
        get => _paymentTerms;
        set => SetProperty(ref _paymentTerms, value);
    }

    public ObservableCollection<Models.MappingProfile> Profiles { get; } = new();

    public Models.MappingProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile != value)
            {
                SetProperty(ref _selectedProfile, value);
                DeleteProfileCommand.RaiseCanExecuteChanged();
                SetActiveProfileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex != value)
            {
                if (IsModified)
                {
                    bool discard = _dialogService.ShowPendingChangesDialog();
                    if (discard)
                    {
                        LoadSettings();
                    }
                    else
                    {
                        OnPropertyChanged(nameof(SelectedTabIndex));
                        return;
                    }
                }
                SetProperty(ref _selectedTabIndex, value);
            }
        }
    }

    public bool IsActiveRowDiscountEnabled => ActiveProfile?.Mapping.Data.EnableRowDiscount ?? false;
    public bool IsActiveOrderIdEnabled => ActiveProfile?.Mapping.Header.EnableOrderId ?? false;
    public bool IsActiveCustomerIdEnabled => ActiveProfile?.Mapping.Header.EnableCustomerId ?? false;

    public Models.MappingProfile? ActiveProfile
    {
        get => _activeProfile;
        set
        {
            if (SetProperty(ref _activeProfile, value))
            {
                NotifyActiveProfileChanged();
            }
        }
    }

    public void NotifyActiveProfileChanged()
    {
        OnPropertyChanged(nameof(ActiveProfile));
        OnPropertyChanged(nameof(IsActiveRowDiscountEnabled));
        OnPropertyChanged(nameof(IsActiveOrderIdEnabled));
        OnPropertyChanged(nameof(IsActiveCustomerIdEnabled));
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
                if (!value)
                {
                    _ = CheckBexioConnectionAsync();
                }
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

    public int? AccountId
    {
        get => _accountId;
        set
        {
            if (SetProperty(ref _accountId, value))
            {
                SetModified();
            }
        }
    }

    public int? TaxId
    {
        get => _taxId;
        set
        {
            if (SetProperty(ref _taxId, value))
            {
                SetModified();
            }
        }
    }

    public void AppendLog(string message)
    {
        _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogText = _logBuilder.ToString();
    }
}
