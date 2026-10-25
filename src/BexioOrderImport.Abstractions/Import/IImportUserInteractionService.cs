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
}
