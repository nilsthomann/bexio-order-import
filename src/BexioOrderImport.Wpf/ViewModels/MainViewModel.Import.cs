using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Domain.Models.Bexio;
using BexioOrderImport.Infrastructure.Excel;
using Microsoft.Extensions.Options;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private readonly System.Collections.Generic.List<(DateTime Timestamp, double UploadedCount)> _progressSamples = new();

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
            OrderId = _loadedOrder.OrderId?.ToString() ?? Resources.Translations.Import_NoOrderId;
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
            TotalsSummary = string.Empty;
            TotalQuantity = 0;
            TotalGrossAmount = 0;
            DiscountPercentVal = 0;
            DiscountAmount = 0;
            TotalNetAmount = 0;
            return;
        }

        // Recalculate based on currently edited positions in the grid
        TotalQuantity = OrderPositions.Sum(p => p.Quantity);
        TotalGrossAmount = OrderPositions.Sum(p => p.TotalPrice);
        DiscountPercentVal = _loadedOrder.DiscountPercent;
        DiscountAmount = TotalGrossAmount * (DiscountPercentVal / 100m);
        TotalNetAmount = TotalGrossAmount - DiscountAmount;

        TotalsSummary = $"{Resources.Translations.Import_SummaryQuantity}: {TotalQuantity} | {Resources.Translations.Import_SummaryGross}: {TotalGrossAmount:F2} CHF | {Resources.Translations.Import_SummaryDiscount}: {DiscountPercentVal}% | {Resources.Translations.Import_SummaryNet}: {TotalNetAmount:F2} CHF";
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
            var useCase = new ImportOrderUseCase(new Services.InMemoryExcelParser(_loadedOrder), bexioClient);

            int createdOrderId = 0;
            bool success = await useCase.ExecuteAsync(
                filePath: SelectedFilePath!,
                showPreviewCallback: order => { }, // Already shown in UI
                confirmUploadCallback: ConfirmUploadAsync,
                confirmCustomerCreationCallback: ConfirmCustomerCreationAsync,
                confirmEmailMismatchCallback: ConfirmEmailMismatchAsync,
                logInfoCallback: message =>
                {
                    InvokeOnUi(() =>
                    {
                        AppendLog(message);
                        var match = System.Text.RegularExpressions.Regex.Match(message, @"Bexio ID:\s*(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                        {
                            createdOrderId = id;
                        }
                    });
                },
                progressCallback: (uploaded, total) =>
                {
                    InvokeOnUi(() =>
                    {
                        currentUploaded = uploaded;
                        currentTotal = total;
                        ProgressPercentage = ((double)uploaded / total) * 100;
                        _progressSamples.Add((DateTime.UtcNow, uploaded));

                        // Keep samples from the last 3 minutes to maintain memory efficiency
                        var cutoff = DateTime.UtcNow.AddMinutes(-3);
                        _progressSamples.RemoveAll(s => s.Timestamp < cutoff);

                        UpdateRemainingTime(currentUploaded, currentTotal, importStopwatch);
                    });
                },
                defaultOrderName: DefaultOrderName,
                seasonCode: SeasonCode,
                positionTextTemplate: PositionTextTemplate
            );

            if (success)
            {
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

        if (uploaded <= 0 || total <= 0 || uploaded >= total)
        {
            RemainingTimeText = string.Format(Resources.Translations.Import_ProgressTimeElapsedOnly, elapsedStr);
            return;
        }

        // If we have fewer than 2 samples or fewer than 3 items uploaded, show estimating
        if (_progressSamples.Count < 2 || uploaded < 3)
        {
            RemainingTimeText = string.Format(Resources.Translations.Import_ProgressTime, elapsedStr, Resources.Translations.Import_EstimatingTime);
            return;
        }

        DateTime now = DateTime.UtcNow;

        // Take a reference sample from ~15-20 positions ago (or at least 5 seconds ago)
        // to calculate the current rolling velocity per position rather than the overall average.
        var referenceSample = _progressSamples
            .Where(s => s.UploadedCount <= uploaded - 10 && (now - s.Timestamp).TotalSeconds >= 5)
            .LastOrDefault();

        if (referenceSample.UploadedCount == 0 && _progressSamples.Count > 0)
        {
            referenceSample = _progressSamples.First();
        }

        double deltaItems = uploaded - referenceSample.UploadedCount;
        double deltaSeconds = (now - referenceSample.Timestamp).TotalSeconds;

        double secondsPerItem;
        if (deltaItems > 0 && deltaSeconds > 0)
        {
            secondsPerItem = deltaSeconds / deltaItems;
        }
        else
        {
            secondsPerItem = elapsedTs.TotalSeconds / uploaded;
        }

        double remainingItems = total - uploaded;
        double remainingSeconds = Math.Max(0, remainingItems * secondsPerItem);

        TimeSpan remainingTs = TimeSpan.FromSeconds(remainingSeconds);
        string formattedRemaining;
        if (remainingTs.TotalMinutes < 1)
        {
            formattedRemaining = $"~{Math.Max(1, (int)Math.Ceiling(remainingSeconds))}s";
        }
        else
        {
            formattedRemaining = $"~{(int)remainingTs.TotalMinutes}m {remainingTs.Seconds}s";
        }

        RemainingTimeText = string.Format(Resources.Translations.Import_ProgressTime, elapsedStr, formattedRemaining);
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
        ConnectionStatusColor = "#F59E0B"; // Yellow warning

        try
        {
            var client = _bexioClientFactory.Create(BexioToken, AccountId, TaxId, SelectedLanguage);
            bool isConnected = await client.CheckConnectionAsync();

            IsConnectionSuccessful = isConnected;

            if (isConnected)
            {
                ConnectionStatusText = Resources.Translations.Status_BexioConnected;
                ConnectionStatusColor = "#10B981"; // Green success
                await LoadBexioOptionsAsync(client);
            }
            else
            {
                ConnectionStatusText = Resources.Translations.Status_BexioDisconnected;
                ConnectionStatusColor = "#EF4444"; // Red error
                ClearBexioOptionsKeepSelected();
            }
        }
        catch
        {
            IsConnectionSuccessful = false;
            ConnectionStatusText = Resources.Translations.Status_BexioDisconnected;
            ConnectionStatusColor = "#EF4444"; // Red error
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
}
