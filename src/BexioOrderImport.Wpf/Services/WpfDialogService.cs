using System;
using System.Windows;
using BexioOrderImport.Domain.Models;

namespace BexioOrderImport.Wpf.Services;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class WpfDialogService : IDialogService
{
    private T InvokeOnDispatcher<T>(Func<T> func)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            return System.Windows.Application.Current.Dispatcher.Invoke(func);
        }
        return func();
    }

    private void InvokeOnDispatcher(Action action)
    {
        if (System.Windows.Application.Current != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(action);
            return;
        }
        action();
    }

    public string? ShowProfileCreateDialog(bool isClone)
    {
        return InvokeOnDispatcher(() =>
        {
            var dialog = new Views.ProfileCreateDialog(isClone);
            dialog.Owner = System.Windows.Application.Current?.MainWindow;
            return dialog.ShowDialog() == true ? dialog.ProfileName : null;
        });
    }

    public bool ShowProfileEditDialog(Models.MappingProfile profile)
    {
        return InvokeOnDispatcher(() =>
        {
            var editWindow = new Views.ProfileEditWindow(profile);
            editWindow.Owner = System.Windows.Application.Current?.MainWindow;
            return editWindow.ShowDialog() == true;
        });
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
            var dialog = new Views.CustomerConfirmWindow(customer);
            dialog.Owner = System.Windows.Application.Current?.MainWindow;
            return dialog.ShowDialog() == true;
        });
    }

    public void ShowErrorDialog(string message, string title)
    {
        InvokeOnDispatcher(() => Views.CustomDialog.ShowError(message, title));
    }

    public void ShowInfoDialog(string message)
    {
        InvokeOnDispatcher(() => Views.CustomDialog.ShowInfo(message));
    }
}
