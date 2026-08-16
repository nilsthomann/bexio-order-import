using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private readonly System.Collections.Generic.List<(DateTime Timestamp, double UploadedCount)> _progressSamples = new();
    private double _smoothedSecondsPerItem;
    private string _lastFormattedRemaining = "-";

    private const string ConnectionColorConnected    = "#10B981"; // Green
    private const string ConnectionColorDisconnected = "#EF4444"; // Red
    private const string ConnectionColorChecking     = "#F59E0B"; // Amber

    public async Task LoadExcelFileAsync(string? filePath = null)
    {
        if (filePath == null)
        {
            filePath = _dialogService.ShowOpenFileDialog("Excel Files|*.xlsx;*.xls", ".xlsx");
        }

        if (string.IsNullOrEmpty(filePath)) return;

        SelectedFilePath = filePath;
        AppendLog(string.Format("Reading Excel file: {0}", Path.GetFileName(filePath)));
        IsLoading = true;

        try
        {
            var options = BuildMappingOptions();
            var parser = _excelParserFactory.Create(options);

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
            OrderId = _loadedOrder.OrderId?.ToString() ?? Resources.Translations.Import_NoOrderId;
            CustomerId = _loadedOrder.CustomerId?.ToString() ?? Resources.Translations.Import_NoCustomerId;
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
            AppendLog($"⛔ Error reading Excel file: {ex.Message}");
            _loadedOrder = null;
            HasLoadedFile = false;
            ImportCommand.RaiseCanExecuteChanged();

            if (IsFileLockedException(ex))
            {
                string fileName = !string.IsNullOrEmpty(filePath) ? Path.GetFileName(filePath) : "Excel";
                string title = Resources.Translations.Import_FileLockedTitle;
                string message = string.Format(Resources.Translations.Import_FileLockedMessage, fileName);
                _dialogService.ShowErrorDialog(message, title);
            }
            else
            {
                _dialogService.ShowErrorDialog(ex.Message, Resources.Translations.Dialog_ErrorTitle);
            }
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
            TotalQuantity = 0;
            TotalGrossAmount = 0;
            DiscountPercentVal = 0;
            TotalNetAmount = 0;
            return;
        }

        // Recalculate based on currently edited positions in the grid
        TotalQuantity = OrderPositions.Sum(p => p.Quantity);
        TotalGrossAmount = OrderPositions.Sum(p => p.TotalPrice);
        DiscountPercentVal = _loadedOrder.DiscountPercent;
        decimal discountAmount = TotalGrossAmount * (DiscountPercentVal / 100m);
        TotalNetAmount = TotalGrossAmount - discountAmount;
    }

    private void ClearLoadedFileInternal(string logMessage)
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
        OrderId = string.Empty;
        CustomerId = string.Empty;
        PaymentTerms = string.Empty;
        OrderPositions.Clear();
        UpdateTotalsSummary();
        ImportCommand.RaiseCanExecuteChanged();
        AppendLog(logMessage);
    }

    private void ClearLoadedFile() => ClearLoadedFileInternal("File upload was deleted by user.");

    private async Task ImportToBexioAsync()
    {
        if (_loadedOrder == null) return;

        if (!AccountId.HasValue || !TaxId.HasValue)
        {
            _dialogService.ShowErrorDialog(
                Resources.Translations.Error_SelectAccountAndTax,
                Resources.Translations.Dialog_ErrorTitle);
            return;
        }

        IsImporting = true;
        IsImportingActive = true;
        LogText = string.Empty;
        ProgressPercentage = 0;
        RemainingTimeText = string.Empty;
        AppendLog("Starting import process...");

        _progressSamples.Clear();
        _smoothedSecondsPerItem = 0;
        _lastFormattedRemaining = "-";
        var importStopwatch = System.Diagnostics.Stopwatch.StartNew();
        double currentUploaded = 0;
        double currentTotal = 0;

        // Periodic timer to tick elapsed time every second on UI
        var uiTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        uiTimer.Tick += (s, e) =>
        {
            UpdateRemainingTime(currentUploaded, currentTotal, importStopwatch);
        };
        uiTimer.Start();

        try
        {
            // Sync values from DataGrid back to Order Positions
            _loadedOrder.Positions = OrderPositions.ToList();

            var bexioClient = _bexioClientFactory.Create(BexioToken, AccountId, TaxId, SelectedLanguage);
            var useCase = new ImportOrderUseCase(bexioClient);

            var mappingOpts = BuildMappingOptions();
            var interaction = new WpfImportUserInteractionService(
                this,
                (uploaded, total, sw) => UpdateRemainingTime(uploaded, total, sw),
                importStopwatch);

            var options = new Application.Models.ImportOrderOptions(
                DefaultOrderName: mappingOpts.DefaultOrderName,
                SeasonCode: mappingOpts.SeasonCode,
                PositionTextTemplate: mappingOpts.PositionTextTemplate,
                DiscountPositionTextTemplate: mappingOpts.DiscountPositionTextTemplate
            );

            var result = await useCase.ExecuteAsync(_loadedOrder, interaction, options);

            if (result.Success)
            {
                int createdOrderId = result.OrderId ?? 0;
                importStopwatch.Stop();
                TimeSpan duration = importStopwatch.Elapsed;
                string formattedDuration = string.Format("{0:D2}:{1:D2} Min", (int)duration.TotalMinutes, duration.Seconds);

                ProgressPercentage = 100;
                RemainingTimeText = string.Empty;
                InvokeOnUi(() =>
                {
                    ImportSuccessTitle = Resources.Translations.Import_SuccessTitle;
                    ImportSuccessMessage = string.Format(Resources.Translations.Import_SuccessMessage, createdOrderId > 0 ? createdOrderId.ToString() : "?");
                    ImportDurationText = string.Format(Resources.Translations.Import_SuccessDuration, formattedDuration);
                    IsImportSuccess = true;
                });
            }
            else
            {
                ProgressPercentage = 0;
                RemainingTimeText = string.Empty;
                AppendLog("Import cancelled. File remains loaded.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"⛔ Error during import: {ex.Message}");
            if (IsFileLockedException(ex))
            {
                string fileName = !string.IsNullOrEmpty(SelectedFilePath) ? Path.GetFileName(SelectedFilePath) : "Excel";
                string title = Resources.Translations.Import_FileLockedTitle;
                string message = string.Format(Resources.Translations.Import_FileLockedMessage, fileName);
                _dialogService.ShowErrorDialog(message, title);
            }
            else
            {
                _dialogService.ShowErrorDialog(ex.Message, Resources.Translations.Dialog_ErrorTitle);
            }
        }
        finally
        {
            uiTimer.Stop();
            IsImporting = false;
            if (!IsImportSuccess)
            {
                IsImportingActive = false;
            }
            RemainingTimeText = string.Empty;
        }
    }

    private static bool IsFileLockedException(Exception ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (current is System.IO.IOException ioEx)
            {
                int hr = System.Runtime.InteropServices.Marshal.GetHRForException(ioEx) & 0xFFFF;
                if (hr == 32 || hr == 33) return true; // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION

                string msg = ioEx.Message;
                if (msg.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("wird von einem anderen Prozess verwendet", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("Prozess kann nicht auf die Datei zugreifen", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            current = current.InnerException;
        }
        return false;
    }

    private void UpdateRemainingTime(double uploaded, double total, System.Diagnostics.Stopwatch stopwatch)
    {
        TimeSpan elapsedTs = stopwatch.Elapsed;
        string elapsedStr = string.Format("{0:D2}:{1:D2}", (int)elapsedTs.TotalMinutes, elapsedTs.Seconds);

        if (total > 0 && uploaded >= total)
        {
            RemainingTimeText = string.Format(Resources.Translations.Import_ProgressTimeElapsedOnly, elapsedStr);
            return;
        }

        if (uploaded >= 3 && _progressSamples.Count >= 2)
        {
            DateTime now = DateTime.UtcNow;

            // Calculate overall average seconds per item
            double overallSecondsPerItem = elapsedTs.TotalSeconds / Math.Max(1, uploaded);

            // Find a reference sample from 4+ seconds ago for rolling velocity
            var validSamples = _progressSamples
                .Where(s => (now - s.Timestamp).TotalSeconds >= 4 && s.UploadedCount < uploaded)
                .ToList();

            double currentInstantSecondsPerItem = overallSecondsPerItem;
            if (validSamples.Count > 0)
            {
                var refSample = validSamples.Last();
                double dItems = uploaded - refSample.UploadedCount;
                double dSec = (now - refSample.Timestamp).TotalSeconds;
                if (dItems > 0 && dSec > 0)
                {
                    currentInstantSecondsPerItem = dSec / dItems;
                }
            }

            // Blend overall average (40%) and recent velocity (60%) for stability
            double targetSecondsPerItem = (overallSecondsPerItem * 0.4) + (currentInstantSecondsPerItem * 0.6);

            // Apply Exponential Moving Average (EMA) smoothing to prevent UI flickering
            if (_smoothedSecondsPerItem <= 0)
            {
                _smoothedSecondsPerItem = targetSecondsPerItem;
            }
            else
            {
                _smoothedSecondsPerItem = (_smoothedSecondsPerItem * 0.85) + (targetSecondsPerItem * 0.15);
            }

            double remainingItems = total - uploaded;
            double remainingSeconds = Math.Max(0, remainingItems * _smoothedSecondsPerItem);

            TimeSpan remainingTs = TimeSpan.FromSeconds(remainingSeconds);
            if (remainingTs.TotalMinutes < 1)
            {
                _lastFormattedRemaining = $"~{Math.Max(1, (int)Math.Ceiling(remainingSeconds))}s";
            }
            else
            {
                _lastFormattedRemaining = $"~{(int)remainingTs.TotalMinutes}m {remainingTs.Seconds}s";
            }
        }

        RemainingTimeText = string.Format(Resources.Translations.Import_ProgressTime, elapsedStr, _lastFormattedRemaining);
    }

    private async Task<bool> ConfirmUploadAsync()
    {
        IsImportingActive = false;
        try
        {
            return _dialogService.ShowConfirmDialog(Resources.Translations.Import_ConfirmMessage, Resources.Translations.Import_ConfirmTitle);
        }
        finally
        {
            if (IsImporting) IsImportingActive = true;
        }
    }

    private async Task<bool> ConfirmCustomerCreationAsync(Customer customer)
    {
        IsImportingActive = false;
        try
        {
            return _dialogService.ShowCustomerConfirmDialog(customer);
        }
        finally
        {
            if (IsImporting) IsImportingActive = true;
        }
    }

    private async Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail)
    {
        IsImportingActive = false;
        try
        {
            string message = string.Format(Resources.Translations.Import_EmailMismatchMessage, existingEmail, excelEmail);
            return _dialogService.ShowConfirmDialog(message, Resources.Translations.Import_EmailMismatchTitle);
        }
        finally
        {
            if (IsImporting) IsImportingActive = true;
        }
    }

    public async Task CheckBexioConnectionAsync()
    {
        ConnectionStatusText = Resources.Translations.Status_BexioChecking;
        ConnectionStatusColor = ConnectionColorChecking;

        try
        {
            var client = _bexioClientFactory.Create(BexioToken, AccountId, TaxId, SelectedLanguage);
            bool isConnected = await client.CheckConnectionAsync();

            IsConnectionSuccessful = isConnected;

            if (isConnected)
            {
                ConnectionStatusText = Resources.Translations.Status_BexioConnected;
                ConnectionStatusColor = ConnectionColorConnected;
                await LoadBexioOptionsAsync(client);
            }
            else
            {
                ConnectionStatusText = Resources.Translations.Status_BexioDisconnected;
                ConnectionStatusColor = ConnectionColorDisconnected;
                ClearBexioOptionsKeepSelected();
            }
        }
        catch
        {
            IsConnectionSuccessful = false;
            ConnectionStatusText = Resources.Translations.Status_BexioDisconnected;
            ConnectionStatusColor = ConnectionColorDisconnected;
            ClearBexioOptionsKeepSelected();
        }
    }

    private async Task LoadBexioOptionsAsync(IBexioClient client)
    {
        try
        {
            var tempAccountId = AccountId;
            var accounts = await client.GetAccountsAsync();
            AccountsList.Clear();
            foreach (var acc in accounts)
                AccountsList.Add(acc);

            if (AccountsList.Count == 0 && !AccountId.HasValue)
            {
                AccountsList.Add(new BexioAccount { AccountNo = string.Empty, Name = string.Empty });
            }
            else
            {
                AccountId = tempAccountId;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"⚠️ Could not load accounts: {ex.Message}");
            if (AccountsList.Count == 0 && !AccountId.HasValue)
            {
                AccountsList.Add(new BexioAccount { AccountNo = string.Empty, Name = string.Empty });
            }
            else if (AccountsList.Count == 0)
            {
                AccountsList.Add(new BexioAccount { Id = AccountId!.Value, AccountNo = AccountId.Value.ToString(), Name = string.Empty });
            }
        }

        try
        {
            var tempTaxId = TaxId;
            var taxes = await client.GetTaxesAsync();
            TaxesList.Clear();
            foreach (var tax in taxes)
                TaxesList.Add(tax);

            if (TaxesList.Count == 0 && !TaxId.HasValue)
            {
                TaxesList.Add(new BexioTax { DisplayName = string.Empty });
            }
            else
            {
                TaxId = tempTaxId;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"⚠️ Could not load tax rates: {ex.Message}");
            if (TaxesList.Count == 0 && !TaxId.HasValue)
            {
                TaxesList.Add(new BexioTax { DisplayName = string.Empty });
            }
            else if (TaxesList.Count == 0)
            {
                TaxesList.Add(new BexioTax { Id = TaxId!.Value, DisplayName = TaxId.Value.ToString() });
            }
        }
    }

    private void ClearBexioOptionsKeepSelected()
    {
        var selectedAccount = AccountsList.FirstOrDefault(x => x.Id == AccountId);
        AccountsList.Clear();
        if (AccountId.HasValue)
        {
            AccountsList.Add(selectedAccount ?? new BexioAccount { Id = AccountId.Value, AccountNo = AccountId.Value.ToString(), Name = string.Empty });
        }

        var selectedTax = TaxesList.FirstOrDefault(x => x.Id == TaxId);
        TaxesList.Clear();
        if (TaxId.HasValue)
        {
            TaxesList.Add(selectedTax ?? new BexioTax { Id = TaxId.Value, DisplayName = TaxId.Value.ToString() });
        }
    }

    private class WpfImportUserInteractionService : IImportUserInteractionService
    {
        private readonly MainViewModel _vm;
        private readonly Action<double, double, System.Diagnostics.Stopwatch> _updateProgressAction;
        private readonly System.Diagnostics.Stopwatch _stopwatch;

        public WpfImportUserInteractionService(
            MainViewModel vm,
            Action<double, double, System.Diagnostics.Stopwatch> updateProgressAction,
            System.Diagnostics.Stopwatch stopwatch)
        {
            _vm = vm;
            _updateProgressAction = updateProgressAction;
            _stopwatch = stopwatch;
        }

        public void ShowPreview(Order order) { }

        public Task<bool> ConfirmUploadAsync() => _vm.ConfirmUploadAsync();

        public Task<bool> ConfirmCustomerCreationAsync(Customer customer) => _vm.ConfirmCustomerCreationAsync(customer);

        public Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail) => _vm.ConfirmEmailMismatchAsync(existingEmail, excelEmail);

        public void LogInfo(string message) => _vm.InvokeOnUi(() => _vm.AppendLog(message));

        public void ReportProgress(int current, int total)
        {
            _vm.InvokeOnUi(() =>
            {
                _vm.ProgressPercentage = ((double)current / total) * 100;
                _vm._progressSamples.Add((DateTime.UtcNow, current));
                var cutoff = DateTime.UtcNow.AddMinutes(-3);
                _vm._progressSamples.RemoveAll(s => s.Timestamp < cutoff);
                _updateProgressAction(current, total, _stopwatch);
            });
        }
    }
}

