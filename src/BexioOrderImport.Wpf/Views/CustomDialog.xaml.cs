using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Views;

public enum CustomDialogType { Info, Warning, Error, Confirm, PendingChanges }

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class CustomDialog : Window
{
    public CustomDialog()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        SyncToOwner();
        if (Owner != null)
        {
            Owner.LocationChanged += (_, _) => SyncToOwner();
            Owner.SizeChanged    += (_, _) => SyncToOwner();
        }
    }

    private void SyncToOwner()
    {
        if (Owner != null)
        {
            var rect = Helpers.WindowHelper.GetAbsolutePlacement(Owner);
            Left   = rect.Left;
            Top    = rect.Top;
            Width  = rect.Width;
            Height = rect.Height;
        }
        else
        {
            Width  = System.Windows.SystemParameters.PrimaryScreenWidth;
            Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            Left   = 0;
            Top    = 0;
        }
    }

    private void ConfigureDialog(CustomDialogType type, string title, string message)
    {
        TitleText.Text = title;
        MessageText.Text = message;

        switch (type)
        {
            case CustomDialogType.Info:
                OkButton.Content = Translations.Dialog_OK;
                OkButton.Style = (Style)FindResource("ModernButtonStyle");
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case CustomDialogType.Warning:
                OkButton.Content = Translations.Dialog_OK;
                OkButton.Style = (Style)FindResource("ModernButtonStyle");
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case CustomDialogType.Error:
                OkButton.Content = Translations.Dialog_OK;
                OkButton.Style = (Style)FindResource("ModernButtonStyle");
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case CustomDialogType.Confirm:
                OkButton.Content = Translations.Dialog_Yes;
                OkButton.Style = (Style)FindResource("ModernButtonStyle");
                CancelButton.Content = Translations.Dialog_No;
                CancelButton.Visibility = Visibility.Visible;
                break;
            case CustomDialogType.PendingChanges:
                OkButton.Content = Translations.Dialog_DiscardChanges;
                OkButton.Style = (Style)FindResource("ModernButtonStyle");
                OkButton.Width = 160;
                CancelButton.Content = Translations.Dialog_Cancel;
                CancelButton.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ── Static factory methods ──────────────────────────────────────────────

    private static bool? _isUnitTest;
    private static bool IsUnitTest()
    {
        if (!_isUnitTest.HasValue)
        {
            _isUnitTest = false;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.FullName?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("xunit") || name.Contains("test") || name.Contains("nunit") || name.Contains("runner"))
                {
                    _isUnitTest = true;
                    break;
                }
            }
        }
        return _isUnitTest.Value;
    }

    public static void ShowInfo(string message, string? title = null)
    {
        if (IsUnitTest()) return;
        var dlg = CreateDialog(CustomDialogType.Info, title ?? Translations.Dialog_InfoTitle, message);
        dlg.ShowDialog();
    }

    public static void ShowWarning(string message, string? title = null)
    {
        if (IsUnitTest()) return;
        var dlg = CreateDialog(CustomDialogType.Warning, title ?? Translations.Dialog_WarningTitle, message);
        dlg.ShowDialog();
    }

    public static void ShowError(string message, string? title = null)
    {
        if (IsUnitTest()) return;
        var dlg = CreateDialog(CustomDialogType.Error, title ?? Translations.Dialog_ErrorTitle, message);
        dlg.ShowDialog();
    }

    /// <summary>
    /// Error with a note below the message whose {0} is a link (eg. to the Bexio order).
    /// </summary>
    public static void ShowError(string message, string title, string note, string linkText, Uri linkUri)
    {
        if (IsUnitTest()) return;
        var dlg = CreateDialog(CustomDialogType.Error, title, message);
        dlg.AddLinkNote(note, linkText, linkUri);
        dlg.ShowDialog();
    }

    private void AddLinkNote(string note, string linkText, Uri linkUri)
    {
        string[] parts = note.Split("{0}", 2);
        var link = new Hyperlink(new Run(linkText)) { NavigateUri = linkUri, Foreground = (Brush)FindResource("AccentBrush") };
        link.RequestNavigate += (_, e) =>
        {
            Helpers.WindowHelper.OpenUrl(e.Uri);
            e.Handled = true;
        };
        MessageText.Inlines.Add(new LineBreak());
        MessageText.Inlines.Add(new LineBreak());
        MessageText.Inlines.Add(new Run(parts[0]));
        MessageText.Inlines.Add(link);
        if (parts.Length > 1) MessageText.Inlines.Add(new Run(parts[1]));
    }

    public static bool ShowConfirm(string message, string? title = null)
    {
        if (IsUnitTest()) return true;
        var dlg = CreateDialog(CustomDialogType.Confirm, title ?? Translations.Dialog_ConfirmTitle, message);
        return dlg.ShowDialog() == true;
    }

    public static bool ShowPendingChanges(string? message = null, string? title = null)
    {
        if (IsUnitTest()) return true;
        var dlg = CreateDialog(
            CustomDialogType.PendingChanges,
            title ?? Translations.Dialog_PendingChangesTitle,
            message ?? Translations.Dialog_PendingChangesMessage);
        return dlg.ShowDialog() == true;
    }

    private static CustomDialog CreateDialog(CustomDialogType type, string title, string message)
    {
        var dlg = new CustomDialog();
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        if (mainWindow != null && mainWindow.IsVisible)
        {
            dlg.Owner = mainWindow;
        }
        dlg.ConfigureDialog(type, title, message);
        return dlg;
    }
}
