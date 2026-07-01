using System;
using System.IO;
using System.Text.Json;
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
    private string? _selectedFilePath;
    private bool _hasLoadedFile;
    private string _selectedFileName = string.Empty;
    private string _fileSizeText = string.Empty;

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
        // Path to CLI appsettings.json or WPF appsettings.json.
        // We will default to a local appsettings.json in the current working directory, or create one.
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        // Copy appsettings.json from CLI directory if exists, or write default
        EnsureAppSettingsFile();

        LoadSettings();
        
        // Commands
        LoadFileCommand = new RelayCommand(async () => await LoadExcelFileAsync());
        ClearFileCommand = new RelayCommand(ClearLoadedFile);
        ImportCommand = new RelayCommand(async () => await ImportToBexioAsync(), () => _loadedOrder != null && !_isImporting);
        SaveSettingsCommand = new RelayCommand(SaveSettings);

        // Async check connection
        _ = CheckBexioConnectionAsync();
    }

    // Commands
    public RelayCommand LoadFileCommand { get; }
    public RelayCommand ClearFileCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }

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

    // Settings view bindings
    public string BexioToken
    {
        get => _bexioToken;
        set => SetProperty(ref _bexioToken, value);
    }

    public int DefaultAccountId
    {
        get => _defaultAccountId;
        set => SetProperty(ref _defaultAccountId, value);
    }

    public int DefaultTaxId
    {
        get => _defaultTaxId;
        set => SetProperty(ref _defaultTaxId, value);
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
        AppendLog(string.Format(Translations.Import_DragDropText.Split(' ')[0] + " Lese Datei ein: {0}", Path.GetFileName(filePath)));

        try
        {
            var options = BuildMappingOptions();
            var parser = new ClosedXmlExcelParser(Options.Create(options));
            _loadedOrder = parser.ParseOrderForm(filePath);
            
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
            AppendLog($"Erfolgreich eingelesen: {_loadedOrder.Positions.Count} Positionen gefunden.");
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] Fehler beim Lesen der Excel-Datei: {ex.Message}");
            _loadedOrder = null;
            HasLoadedFile = false;
            ImportCommand.RaiseCanExecuteChanged();
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
        AppendLog("Datei-Upload wurde vom Benutzer gelöscht.");
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
        AppendLog("Import erfolgreich abgeschlossen. Datei-Auswahl zurückgesetzt.");
    }

    private async Task ImportToBexioAsync()
    {
        if (_loadedOrder == null) return;

        IsImporting = true;
        LogText = string.Empty;
        ProgressPercentage = 0;
        AppendLog("Starte Import-Ablauf...");

        try
        {
            // Sync values from DataGrid back to Order Positions
            _loadedOrder.Positions = OrderPositions.ToList();

            using var httpClient = new HttpClient();
            var bexioClient = new BexioApiClient(httpClient, BexioToken, DefaultAccountId, DefaultTaxId);
            var useCase = new ImportOrderUseCase(new InMemoryExcelParser(_loadedOrder), bexioClient);

            await useCase.ExecuteAsync(
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
            
            ProgressPercentage = 100;
            
            App.Current.Dispatcher.Invoke(() =>
            {
                ClearLoadedFileAfterSuccess();
            });
        }
        catch (Exception ex)
        {
            AppendLog($"[Error] Fehler beim Import: {ex.Message}");
        }
        finally
        {
            IsImporting = false;
        }
    }

    private async Task<bool> ConfirmUploadAsync()
    {
        return await App.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = MessageBox.Show(
                App.Current.MainWindow,
                "Möchten Sie diese Bestellung jetzt an Bexio übermitteln?",
                Translations.MainWindow_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        });
    }

    private async Task<bool> ConfirmCustomerCreationAsync(Customer customer)
    {
        return await App.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new CustomerConfirmWindow(customer);
            dialog.Owner = App.Current.MainWindow;
            return dialog.ShowDialog() == true;
        });
    }

    // Settings persistence
    private void EnsureAppSettingsFile()
    {
        if (!File.Exists(_configFilePath))
        {
            // Write default settings structure
            var defaultSettings = new
            {
                Bexio = new { ApiToken = "bexio_api_token_here", DefaultAccountId = 3200, DefaultTaxId = 1 },
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
            };
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private void LoadSettings()
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_configFilePath));
            var root = doc.RootElement;

            if (root.TryGetProperty("Bexio", out var bexio))
            {
                BexioToken = bexio.GetProperty("ApiToken").GetString() ?? "";
                DefaultAccountId = bexio.GetProperty("DefaultAccountId").GetInt32();
                DefaultTaxId = bexio.GetProperty("DefaultTaxId").GetInt32();
            }

            if (root.TryGetProperty("ExcelMapping", out var mapping))
            {
                var header = mapping.GetProperty("Header");
                CompanyNameCell = header.GetProperty("CompanyNameCell").GetString() ?? "B4";
                StreetCell = header.GetProperty("StreetCell").GetString() ?? "B5";
                ZipCityCell = header.GetProperty("ZipCityCell").GetString() ?? "B6";
                BuyerEmailCell = header.GetProperty("BuyerEmailCell").GetString() ?? "E5";
                BuyerNameCell = header.GetProperty("BuyerNameCell").GetString() ?? "E4";
                DeliveryDateCell = header.GetProperty("DeliveryDateCell").GetString() ?? "T7";
                PaymentTermsCell = header.GetProperty("PaymentTermsCell").GetString() ?? "A9";
                
                if (header.TryGetProperty("DiscountCell", out var discCell))
                {
                    DiscountCell = discCell.GetString() ?? "V12";
                }

                var matrix = mapping.GetProperty("SizeMatrix");
                MatrixStartRow = matrix.GetProperty("StartRow").GetInt32();
                MatrixEndRow = matrix.GetProperty("EndRow").GetInt32();
                MatrixCategoryCol = matrix.GetProperty("CategoryColumn").GetInt32();
                MatrixStartSizeCol = matrix.GetProperty("StartSizeColumn").GetInt32();
                MatrixEndSizeCol = matrix.GetProperty("EndSizeColumn").GetInt32();

                var data = mapping.GetProperty("Data");
                DataStartRow = data.GetProperty("StartRow").GetInt32();
                ColArtNum = data.GetProperty("ArticleNumberColumn").GetInt32();
                ColArtName = data.GetProperty("ArticleNameColumn").GetInt32();
                ColColor = data.GetProperty("ColorColumn").GetInt32();
                ColSizeCategory = data.GetProperty("CategoryColumn").GetInt32();
                ColStartQty = data.GetProperty("StartQtyColumn").GetInt32();
                ColEndQty = data.GetProperty("EndQtyColumn").GetInt32();
                ColUnitPrice = data.GetProperty("UnitPriceColumn").GetInt32();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Laden der Einstellungen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settingsObj = new
            {
                Bexio = new { ApiToken = BexioToken, DefaultAccountId = DefaultAccountId, DefaultTaxId = DefaultTaxId },
                ExcelMapping = new
                {
                    WorksheetIndex = 1,
                    Header = new
                    {
                        CompanyNameCell = CompanyNameCell,
                        StreetCell = StreetCell,
                        ZipCityCell = ZipCityCell,
                        BuyerEmailCell = BuyerEmailCell,
                        BuyerNameCell = BuyerNameCell,
                        DeliveryDateCell = DeliveryDateCell,
                        PaymentTermsCell = PaymentTermsCell,
                        DiscountCell = DiscountCell
                    },
                    SizeMatrix = new
                    {
                        StartRow = MatrixStartRow,
                        EndRow = MatrixEndRow,
                        CategoryColumn = MatrixCategoryCol,
                        StartSizeColumn = MatrixStartSizeCol,
                        EndSizeColumn = MatrixEndSizeCol
                    },
                    Data = new
                    {
                        StartRow = DataStartRow,
                        ArticleNumberColumn = ColArtNum,
                        ArticleNameColumn = ColArtName,
                        ColorColumn = ColColor,
                        CategoryColumn = ColSizeCategory,
                        StartQtyColumn = ColStartQty,
                        EndQtyColumn = ColEndQty,
                        UnitPriceColumn = ColUnitPrice
                    }
                }
            };

            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true }));
            
            // Reload token connection status
            _ = CheckBexioConnectionAsync();

            // Reload the active Excel file to apply configuration changes (e.g. updated discount cell coordinates)
            if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }

            MessageBox.Show(Translations.Settings_SaveSuccess, Translations.Import_SuccessTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            AppendLog("Einstellungen wurden erfolgreich gespeichert und aktivierte Excel-Datei neu eingelesen.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Speichern der Einstellungen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ExcelMappingOptions BuildMappingOptions()
    {
        return new ExcelMappingOptions
        {
            WorksheetIndex = 1,
            Header = new HeaderMapping
            {
                CompanyNameCell = CompanyNameCell,
                StreetCell = StreetCell,
                ZipCityCell = ZipCityCell,
                BuyerEmailCell = BuyerEmailCell,
                BuyerNameCell = BuyerNameCell,
                DeliveryDateCell = DeliveryDateCell,
                PaymentTermsCell = PaymentTermsCell,
                DiscountCell = DiscountCell
            },
            SizeMatrix = new SizeMatrixMapping
            {
                StartRow = MatrixStartRow,
                EndRow = MatrixEndRow,
                CategoryColumn = MatrixCategoryCol,
                StartSizeColumn = MatrixStartSizeCol,
                EndSizeColumn = MatrixEndSizeCol
            },
            Data = new DataMapping
            {
                StartRow = DataStartRow,
                ArticleNumberColumn = ColArtNum,
                ArticleNameColumn = ColArtName,
                ColorColumn = ColColor,
                CategoryColumn = ColSizeCategory,
                StartQtyColumn = ColStartQty,
                EndQtyColumn = ColEndQty,
                UnitPriceColumn = ColUnitPrice
            }
        };
    }
}

public class InMemoryExcelParser : IExcelParser
{
    private readonly Order _order;
    public InMemoryExcelParser(Order order) => _order = order;
    public Order ParseOrderForm(string filePath) => _order;
}
