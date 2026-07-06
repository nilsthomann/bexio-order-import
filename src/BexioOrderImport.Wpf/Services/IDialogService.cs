using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.Services;

public interface IDialogService
{
    string? ShowProfileCreateDialog(bool isClone);
    bool ShowProfileEditDialog(Models.MappingProfile profile);
    string? ShowOpenFileDialog(string filter, string defaultExt);
    string? ShowSaveFileDialog(string filter, string defaultExt, string defaultFileName);
    bool ShowConfirmDialog(string message, string title);
    bool ShowCustomerConfirmDialog(Customer customer);
    void ShowErrorDialog(string message, string title);
    void ShowInfoDialog(string message);
}
