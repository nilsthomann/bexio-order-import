using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Services;

public class DelegateImportUserInteractionService : IImportUserInteractionService
{
    private readonly Action<Order>? _showPreview;
    private readonly Func<Task<bool>>? _confirmUpload;
    private readonly Func<Customer, Task<bool>>? _confirmCustomerCreation;
    private readonly Func<string, string, Task<bool>>? _confirmEmailMismatch;
    private readonly Action<string>? _logInfo;
    private readonly Action<int, int>? _reportProgress;

    public DelegateImportUserInteractionService(
        Action<Order>? showPreview = null,
        Func<Task<bool>>? confirmUpload = null,
        Func<Customer, Task<bool>>? confirmCustomerCreation = null,
        Func<string, string, Task<bool>>? confirmEmailMismatch = null,
        Action<string>? logInfo = null,
        Action<int, int>? reportProgress = null)
    {
        _showPreview = showPreview;
        _confirmUpload = confirmUpload;
        _confirmCustomerCreation = confirmCustomerCreation;
        _confirmEmailMismatch = confirmEmailMismatch;
        _logInfo = logInfo;
        _reportProgress = reportProgress;
    }

    public void ShowPreview(Order order) => _showPreview?.Invoke(order);
    public Task<bool> ConfirmUploadAsync() => _confirmUpload != null ? _confirmUpload() : Task.FromResult(true);
    public Task<bool> ConfirmCustomerCreationAsync(Customer customer) => _confirmCustomerCreation != null ? _confirmCustomerCreation(customer) : Task.FromResult(true);
    public Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail) => _confirmEmailMismatch != null ? _confirmEmailMismatch(existingEmail, excelEmail) : Task.FromResult(true);
    public void LogInfo(string message) => _logInfo?.Invoke(message);
    public void ReportProgress(int current, int total) => _reportProgress?.Invoke(current, total);
}
