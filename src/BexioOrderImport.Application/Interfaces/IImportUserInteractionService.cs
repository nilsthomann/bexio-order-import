using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Application.Interfaces;

public interface IImportUserInteractionService
{
    void ShowPreview(Order order);
    Task<bool> ConfirmUploadAsync();
    Task<bool> ConfirmCustomerCreationAsync(Customer customer);
    Task<bool> ConfirmEmailMismatchAsync(string existingEmail, string excelEmail);
    void LogInfo(string message);
    void ReportProgress(int current, int total);
}
