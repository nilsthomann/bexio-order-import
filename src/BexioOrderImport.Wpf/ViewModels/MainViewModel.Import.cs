using System.Globalization;
using System.IO;
using BexioOrderImport.Abstractions.Bexio;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Import;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private readonly ImportTimeEstimator _timeEstimator = new();

    private const string ConnectionColorConnected    = "#10B981"; // Green
    private const string ConnectionColorDisconnected = "#EF4444"; // Red
    private const string ConnectionColorChecking     = "#F59E0B"; // Amber

    /// <summary>
    /// Bexio links orders by their id, not by the document number.
    /// </summary>
    public static Uri GetBexioOrderUri(int orderId) =>
        new(string.Create(CultureInfo.InvariantCulture, $"https://office.bexio.com/index.php/kb_order/show/id/{orderId}"));

    private (IFormatAdapter Format, IFormatSettings Settings) GetActiveFormat()
    {
        var format = _formatRegistry.Find(ActiveProfile?.FormatId);
        if (format == null || ActiveProfile?.Settings == null)
        {
            throw new InvalidOperationException(string.Format(Resources.Translations.Error_FormatUnavailable, ActiveProfile?.FormatId));
        }
        return (format, ActiveProfile.Settings);
    }

    public async Task LoadExcelFileAsync(string? filePath = null)
    {
        filePath ??= _dialogService.ShowOpenFileDialog("Excel Files|*.xlsx;*.xlsm", ".xlsx");

        if (string.IsNullOrEmpty(filePath)) return;

        // Dropped files are not filtered; the Excel library reads only the xlsx format
        if (string.Equals(Path.GetExtension(filePath), ".xls", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog($"⛔ Unsupported file format: {Path.GetFileName(filePath)}");
            _dialogService.ShowErrorDialog(string.Format(Resources.Translations.Import_XlsNotSupported, Path.GetFileName(filePath)), Resources.Translations.Dialog_ErrorTitle);
            return;
        }

        SelectedFilePath = filePath;
        AppendLog(string.Format("Reading Excel file: {0}", Path.GetFileName(filePath)));
        IsLoading = true;

        try
        {
            var (format, settings) = GetActiveFormat();

            // Parse on background thread to keep UI responsive and allow spinner animation
            var order = await Task.Run(() => format.Parse(filePath, settings));

            // Populate file info
            var fileInfo = new FileInfo(filePath);
            FileSizeText = $"{fileInfo.Length / 1024.0:F1} KB";
            SelectedFileName = Path.GetFileName(filePath);
            HasLoadedFile = true;

            LoadedOrder = order;
            ImportCommand.RaiseCanExecuteChanged();
            AppendLog($"Successfully read: {order.Positions.Count} positions found.");
        }
        catch (Exception ex)
        {
            AppendLog($"⛔ Error reading Excel file: {ex.Message}");
            LoadedOrder = null;
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

    /// <summary>
    /// Reads the selected file again, eg. with the settings of another profile.
    /// </summary>
    private void ReloadLoadedFile()
    {
        if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
        {
            _ = LoadExcelFileAsync(SelectedFilePath);
        }
    }

    private void ClearLoadedFileInternal(string logMessage)
    {
        LoadedOrder = null;
        SelectedFilePath = null;
        SelectedFileName = string.Empty;
        FileSizeText = string.Empty;
        HasLoadedFile = false;
        ImportCommand.RaiseCanExecuteChanged();
        AppendLog(logMessage);
    }

    private void ClearLoadedFile() => ClearLoadedFileInternal("File upload was deleted by user.");

    private async Task ImportToBexioAsync()
    {
        if (_loadedOrder == null) return;

        if (!AccountId.HasValue || !TaxId.HasValue || !PaymentTypeId.HasValue)
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

        _timeEstimator.Reset();
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

        var interaction = new WpfImportUserInteractionService(
            this,
            (uploaded, total, sw) => UpdateRemainingTime(uploaded, total, sw),
            importStopwatch);

        try
        {
            var (format, settings) = GetActiveFormat();
            var bexioClient = _bexioClientFactory.Create(BexioToken, AccountId, TaxId, PaymentTypeId, SelectedLanguage);

            var result = await format.ImportAsync(_loadedOrder, settings, bexioClient, interaction);

            if (result.Success)
            {
                string displayOrderNr = !string.IsNullOrWhiteSpace(result.OrderNumber)
                    ? result.OrderNumber
                    : "?";

                importStopwatch.Stop();
                TimeSpan duration = importStopwatch.Elapsed;
                string formattedDuration = string.Format("{0:D2}:{1:D2} Min", (int)duration.TotalMinutes, duration.Seconds);

                ProgressPercentage = 100;
                RemainingTimeText = string.Empty;
                InvokeOnUi(() =>
                {
                    ImportSuccessTitle = Resources.Translations.Import_SuccessTitle;
                    ImportedOrderNumber = displayOrderNr;
                    ImportedOrderUri = interaction.ResolvedOrder is { } resolved ? GetBexioOrderUri(resolved.Id) : null;
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
            if (interaction.ResolvedOrder is { } resolved)
            {
                // The order already exists in Bexio, the user has to clean it up
                string note = resolved.IsNew ? Resources.Translations.Import_OrderCreatedError : Resources.Translations.Import_OrderPartlyFilledError;
                _dialogService.ShowImportErrorDialog(ex.Message, Resources.Translations.Dialog_ErrorTitle, note, resolved.DocumentNr, GetBexioOrderUri(resolved.Id));
            }
            else if (IsFileLockedException(ex))
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
                if (hr is 32 or 33) return true; // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION

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
        RemainingTimeText = _timeEstimator.GetTimeText(uploaded, total, stopwatch.Elapsed, DateTime.UtcNow);
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
            var client = _bexioClientFactory.Create(BexioToken, AccountId, TaxId, PaymentTypeId, SelectedLanguage);
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

    private async Task LoadBexioOptionsAsync(IBexioService client)
    {
        int? accountId = AccountId;
        int? taxId = TaxId;
        int? paymentTypeId = PaymentTypeId;
        await _bexioOptions.LoadAsync(client, accountId, taxId, paymentTypeId, AppendLog);
        RestoreBexioSelection(accountId, taxId, paymentTypeId);
    }

    private void ClearBexioOptionsKeepSelected()
    {
        int? accountId = AccountId;
        int? taxId = TaxId;
        int? paymentTypeId = PaymentTypeId;
        _bexioOptions.KeepSelected(accountId, taxId, paymentTypeId);
        RestoreBexioSelection(accountId, taxId, paymentTypeId);
    }

    /// <summary>
    /// Replacing the items of the combo boxes clears their selection, which the binding writes back.
    /// </summary>
    private void RestoreBexioSelection(int? accountId, int? taxId, int? paymentTypeId)
    {
        AccountId = accountId;
        TaxId = taxId;
        PaymentTypeId = paymentTypeId;
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

        public (int Id, string DocumentNr, bool IsNew)? ResolvedOrder { get; private set; }

        public void ShowPreview(Order order) { }

        public void OrderResolved(int orderId, string documentNr, bool isNew) => ResolvedOrder = (orderId, documentNr, isNew);

        public Task<bool> ConfirmUploadAsync() => _vm.ConfirmUploadAsync();

        public Task<bool> ConfirmCustomerCreationAsync(Customer customer) => _vm.ConfirmCustomerCreationAsync(customer);

        public Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail) => _vm.ConfirmEmailMismatchAsync(existingEmail, excelEmail);

        public void LogInfo(string message) => _vm.InvokeOnUi(() => _vm.AppendLog(message));

        public void ReportProgress(int current, int total)
        {
            _vm.InvokeOnUi(() =>
            {
                _vm.ProgressPercentage = ((double)current / total) * 100;
                _vm._timeEstimator.AddSample(current, DateTime.UtcNow);
                _updateProgressAction(current, total, _stopwatch);
            });
        }
    }
}

