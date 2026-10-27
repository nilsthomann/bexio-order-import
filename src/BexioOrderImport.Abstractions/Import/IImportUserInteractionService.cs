using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Abstractions.Import;

public interface IImportUserInteractionService
{
    void ShowPreview(Order order);
    Task<bool> ConfirmUploadAsync();
    Task<bool> ConfirmCustomerCreationAsync(Customer customer);
    Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail);
    void LogInfo(string message);
    void ReportProgress(int current, int total);

    /// <summary>
    /// The Bexio order that receives the positions is known; <paramref name="isNew"/> if the import created it.
    /// </summary>
    void OrderResolved(int orderId, string documentNr, bool isNew);

    /// <summary>
    /// Asks for a text, eg. a customer number. <paramref name="errorMessage"/> explains why the previous input was rejected.
    /// </summary>
    /// <returns>The entered text, or null if the user cancels.</returns>
    Task<string?> AskTextAsync(string title, string message, string? errorMessage = null);

    /// <summary>
    /// Lets the user pick one of the <paramref name="options"/>.
    /// </summary>
    /// <returns>The index of the chosen option, or null if the user cancels.</returns>
    Task<int?> ChooseAsync(string title, string message, IReadOnlyList<string> options);

    /// <summary>
    /// Warns before a change that can't be undone; true if the user accepts it.
    /// </summary>
    Task<bool> ConfirmWarningAsync(string title, string message);

    /// <summary>
    /// The positions of the resolved order were deleted, so a failed import leaves the order without its old positions.
    /// </summary>
    void OrderPositionsDeleted();
}
