using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Abstractions.Import;

/// <summary>
/// Interaction with delegates. Without a delegate the confirmations of the existing flows accept,
/// while text input and choices cancel and warnings are declined, so nothing is deleted by default.
/// </summary>
public class DelegateImportUserInteractionService : IImportUserInteractionService
{
    private readonly Action<Order>? _showPreview;
    private readonly Func<Task<bool>>? _confirmUpload;
    private readonly Func<Customer, Task<bool>>? _confirmCustomerCreation;
    private readonly Func<string, string, Task<bool>>? _confirmEmailMismatch;
    private readonly Action<string>? _logInfo;
    private readonly Action<int, int>? _reportProgress;
    private readonly Action<int, string, bool>? _orderResolved;
    private readonly Func<string, string, string?, Task<string?>>? _askText;
    private readonly Func<string, string, IReadOnlyList<string>, Task<int?>>? _choose;
    private readonly Func<string, string, Task<bool>>? _confirmWarning;
    private readonly Action? _orderPositionsDeleted;

    public DelegateImportUserInteractionService(
        Action<Order>? showPreview = null,
        Func<Task<bool>>? confirmUpload = null,
        Func<Customer, Task<bool>>? confirmCustomerCreation = null,
        Func<string, string, Task<bool>>? confirmEmailMismatch = null,
        Action<string>? logInfo = null,
        Action<int, int>? reportProgress = null,
        Action<int, string, bool>? orderResolved = null,
        Func<string, string, string?, Task<string?>>? askText = null,
        Func<string, string, IReadOnlyList<string>, Task<int?>>? choose = null,
        Func<string, string, Task<bool>>? confirmWarning = null,
        Action? orderPositionsDeleted = null)
    {
        _showPreview = showPreview;
        _confirmUpload = confirmUpload;
        _confirmCustomerCreation = confirmCustomerCreation;
        _confirmEmailMismatch = confirmEmailMismatch;
        _logInfo = logInfo;
        _reportProgress = reportProgress;
        _orderResolved = orderResolved;
        _askText = askText;
        _choose = choose;
        _confirmWarning = confirmWarning;
        _orderPositionsDeleted = orderPositionsDeleted;
    }

    public void ShowPreview(Order order) => _showPreview?.Invoke(order);
    public Task<bool> ConfirmUploadAsync() => _confirmUpload != null ? _confirmUpload() : Task.FromResult(true);
    public Task<bool> ConfirmCustomerCreationAsync(Customer customer) => _confirmCustomerCreation != null ? _confirmCustomerCreation(customer) : Task.FromResult(true);
    public Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail) => _confirmEmailMismatch != null ? _confirmEmailMismatch(existingEmail, excelEmail) : Task.FromResult(true);
    public void LogInfo(string message) => _logInfo?.Invoke(message);
    public void ReportProgress(int current, int total) => _reportProgress?.Invoke(current, total);
    public void OrderResolved(int orderId, string documentNr, bool isNew) => _orderResolved?.Invoke(orderId, documentNr, isNew);
    public Task<string?> AskTextAsync(string title, string message, string? errorMessage = null) => _askText != null ? _askText(title, message, errorMessage) : Task.FromResult<string?>(null);
    public Task<int?> ChooseAsync(string title, string message, IReadOnlyList<string> options) => _choose != null ? _choose(title, message, options) : Task.FromResult<int?>(null);
    public Task<bool> ConfirmWarningAsync(string title, string message) => _confirmWarning != null ? _confirmWarning(title, message) : Task.FromResult(false);
    public void OrderPositionsDeleted() => _orderPositionsDeleted?.Invoke();
}
