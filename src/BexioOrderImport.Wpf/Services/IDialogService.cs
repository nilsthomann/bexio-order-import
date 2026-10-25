using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.Services;

/// <summary>
/// Name and format entered in the create dialog.
/// </summary>
public record ProfileCreateResult(string Name, string FormatId);

public interface IDialogService
{
    /// <summary>
    /// Asks for the name and format of a new profile. A <paramref name="fixedFormatId"/> (when cloning) is shown but can't be changed.
    /// </summary>
    ProfileCreateResult? ShowProfileCreateDialog(string? fixedFormatId);
    bool ShowProfileEditDialog(Models.MappingProfile profile, System.Collections.Generic.IEnumerable<Models.MappingProfile>? existingProfiles = null);
    bool ShowPendingChangesDialog();
    string? ShowOpenFileDialog(string filter, string defaultExt);
    string? ShowSaveFileDialog(string filter, string defaultExt, string defaultFileName);
    bool ShowConfirmDialog(string message, string title);
    bool ShowCustomerConfirmDialog(Customer customer);
    void ShowErrorDialog(string message, string title);

    /// <summary>
    /// Error of an import that already wrote to a Bexio order: <paramref name="note"/> shows the order number at {0} as a link to <paramref name="orderUri"/>.
    /// </summary>
    void ShowImportErrorDialog(string message, string title, string note, string orderNumber, Uri orderUri);
    void ShowInfoDialog(string message);
    void RestartMainWindow();
}
