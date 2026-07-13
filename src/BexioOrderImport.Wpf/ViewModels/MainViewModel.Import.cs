using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Infrastructure.Excel;
using Microsoft.Extensions.Options;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
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
            DeliveryDate = _loadedOrder.DeliveryDate?.ToString("dd.MM.yyyy") ?? Resources.Translations.Import_NoDeliveryDate;
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
        DeliveryDate = string.Empty;
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
        AppendLog("Starting import process...");

        try
        {
            // Sync values from DataGrid back to Order Positions
            _loadedOrder.Positions = OrderPositions.ToList();

            var bexioClient = _bexioClientFactory.Create(BexioToken, AccountId, TaxId);
            var useCase = new ImportOrderUseCase(new Services.InMemoryExcelParser(_loadedOrder), bexioClient);

            int createdOrderId = 0;
            bool success = await useCase.ExecuteAsync(
                filePath: SelectedFilePath!,
                showPreviewCallback: order => { }, // Already shown in UI
                confirmUploadCallback: ConfirmUploadAsync,
                confirmCustomerCreationCallback: ConfirmCustomerCreationAsync,
                logInfoCallback: message =>
                {
                    InvokeOnUi(() =>
                    {
                        AppendLog(message);
                        if (message.Contains("Positions uploaded:"))
                        {
                            var parts = message.Split(':').Last().Trim().Split('/');
                            if (parts.Length == 2 && double.TryParse(parts[0], out double uploaded) && double.TryParse(parts[1], out double total))
                            {
                                ProgressPercentage = (uploaded / total) * 100;
                            }
                        }
                        
                        var match = System.Text.RegularExpressions.Regex.Match(message, @"Bexio ID:\s*(\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var id))
                        {
                            createdOrderId = id;
                        }
                    });
                }
            );

            if (success)
            {
                ProgressPercentage = 100;
                InvokeOnUi(() =>
                {
                    ClearLoadedFileInternal("Import completed successfully. File selection reset.");
                    _dialogService.ShowInfoDialog(string.Format(Resources.Translations.Import_SuccessMessage, createdOrderId > 0 ? createdOrderId.ToString() : "?"));
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
            _dialogService.ShowErrorDialog(ex.Message, Resources.Translations.Dialog_ErrorTitle);
        }
        finally
        {
            IsImporting = false;
            IsImportingActive = false;
        }
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

    public async Task CheckBexioConnectionAsync()
    {
        ConnectionStatusText = Resources.Translations.Status_BexioChecking;
        ConnectionStatusColor = "#F59E0B"; // Yellow warning

        try
        {
            var client = _bexioClientFactory.Create(BexioToken, AccountId, TaxId);
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
            AppendLog($"[Warning] Could not load accounts: {ex.Message}");
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
            AppendLog($"[Warning] Could not load tax rates: {ex.Message}");
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
