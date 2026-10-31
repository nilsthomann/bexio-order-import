using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.Services;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class WpfDialogService : IDialogService
{
    private readonly IFormatRegistry _formats;
    private readonly FormatUiRegistry _formatUis;

    public WpfDialogService(IFormatRegistry formats, FormatUiRegistry formatUis)
    {
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _formatUis = formatUis ?? throw new ArgumentNullException(nameof(formatUis));
    }

    private static T InvokeOnDispatcher<T>(Func<T> func)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(func);
        }
        return func();
    }

    private static void InvokeOnDispatcher(Action action)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
            return;
        }
        action();
    }

    public ProfileCreateResult? ShowProfileCreateDialog(string? fixedFormatId)
    {
        return InvokeOnDispatcher(() =>
        {
            var dialog = new Views.ProfileCreateDialog(_formats.Formats, fixedFormatId)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            return dialog.ShowDialog() == true ? new ProfileCreateResult(dialog.ProfileName, dialog.FormatId) : null;
        });
    }

    public bool ShowProfileEditDialog(Models.MappingProfile profile, System.Collections.Generic.IEnumerable<Models.MappingProfile>? existingProfiles = null)
    {
        var format = _formats.Find(profile.FormatId) ?? throw new InvalidOperationException($"Format '{profile.FormatId}' is not registered.");
        var formatUi = _formatUis.Find(profile.FormatId) ?? throw new InvalidOperationException($"Format '{profile.FormatId}' has no UI provider.");

        return InvokeOnDispatcher(() =>
        {
            var editWindow = new Views.ProfileEditWindow(profile, format, formatUi, existingProfiles)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            return editWindow.ShowDialog() == true;
        });
    }

    public bool ShowPendingChangesDialog()
    {
        return InvokeOnDispatcher(() => Views.CustomDialog.ShowPendingChanges());
    }

    public string? ShowOpenFileDialog(string filter, string defaultExt)
    {
        return InvokeOnDispatcher(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        });
    }

    public string? ShowSaveFileDialog(string filter, string defaultExt, string defaultFileName)
    {
        return InvokeOnDispatcher(() =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                DefaultExt = defaultExt,
                FileName = defaultFileName
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        });
    }

    public bool ShowConfirmDialog(string message, string title)
    {
        return InvokeOnDispatcher(() => Views.CustomDialog.ShowConfirm(message, title));
    }

    public bool ShowCustomerConfirmDialog(Customer customer)
    {
        return InvokeOnDispatcher(() =>
        {
            var dialog = new Views.CustomerConfirmWindow(customer)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            return dialog.ShowDialog() == true;
        });
    }

    public string? ShowInputDialog(string message, string title, string? errorMessage)
    {
        return InvokeOnDispatcher(() => Views.CustomDialog.ShowInput(message, title, errorMessage));
    }

    public int? ShowChoiceDialog(string message, string title, IReadOnlyList<string> options)
    {
        return InvokeOnDispatcher(() => Views.CustomDialog.ShowChoice(message, title, options));
    }

    public void ShowErrorDialog(string message, string title)
    {
        InvokeOnDispatcher(() => Views.CustomDialog.ShowError(message, title));
    }

    public void ShowImportErrorDialog(string message, string title, string note, string orderNumber, Uri orderUri)
    {
        InvokeOnDispatcher(() => Views.CustomDialog.ShowError(message, title, note, orderNumber, orderUri));
    }

    public void ShowInfoDialog(string message)
    {
        InvokeOnDispatcher(() => Views.CustomDialog.ShowInfo(message));
    }

    public void RestartMainWindow()
    {
        InvokeOnDispatcher(() =>
        {
            if (System.Windows.Application.Current is App)
            {
                var newWindow = new Views.MainWindow();
                newWindow.Show();
                System.Windows.Application.Current.MainWindow?.Close();
                System.Windows.Application.Current.MainWindow = newWindow;
            }
        });
    }
}
