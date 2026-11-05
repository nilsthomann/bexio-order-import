using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Views;

public enum CustomDialogType { Info, Warning, Error, Confirm }

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

    private static readonly Color Purple = Color.FromRgb(0x8B, 0x5C, 0xF6), PurpleDark = Color.FromRgb(0x6D, 0x28, 0xD9);
    private static readonly Color Amber = Color.FromRgb(0xF5, 0x9E, 0x0B), AmberDark = Color.FromRgb(0xD9, 0x77, 0x06);
    private static readonly Color Red = Color.FromRgb(0xEF, 0x44, 0x44), RedDark = Color.FromRgb(0xB9, 0x1C, 0x1C);
    // Strokes on the 64x64 icon circle; the short segments are the round dots of "i", "!" and "?"
    private const string InfoIcon = "M32,20 L32,20.01 M32,29 L32,44";
    private const string QuestionIcon = "M25,26 C25,17 39,17 39,25 C39,31 32,31 32,37 M32,44 L32,44.01";
    private const string WarningIcon = "M32,19 L32,36 M32,44 L32,44.01";
    private const string ErrorIcon = "M24,24 L40,40 M40,24 L24,40";

    private void ConfigureDialog(CustomDialogType type, string title, string message)
    {
        TitleText.Text = title;
        SetMessage(message);

        OkButton.Content = Translations.Dialog_OK;
        CancelButton.Content = Translations.Dialog_Cancel;
        switch (type)
        {
            case CustomDialogType.Info:
                SetIcon(Purple, PurpleDark, InfoIcon);
                break;
            case CustomDialogType.Warning:
                SetIcon(Amber, AmberDark, WarningIcon);
                break;
            case CustomDialogType.Error:
                SetIcon(Red, RedDark, ErrorIcon);
                break;
            case CustomDialogType.Confirm:
                SetIcon(Purple, PurpleDark, QuestionIcon);
                OkButton.Content = Translations.Dialog_Yes;
                CancelButton.Content = Translations.Dialog_No;
                CancelButton.Visibility = Visibility.Visible;
                break;
        }
    }

    private void SetIcon(Color from, Color to, string path)
    {
        IconCircle.Fill = new LinearGradientBrush(from, to, 45);
        IconShadow.Color = to;
        IconPath.Data = Geometry.Parse(path);
    }

    /// <summary>
    /// Shows the lines starting with "• " as a list below the rest of the message.
    /// </summary>
    internal void SetMessage(string message)
    {
        string[] lines = message.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var bullets = lines.Where(IsBullet).Select(l => "• " + l[2..].Trim()).ToList();
        SetFormattedText(MessageText, string.Join('\n', lines.Where(l => !IsBullet(l))).Trim());
        MessageText.Visibility = MessageText.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BulletList.Text = string.Join(Environment.NewLine, bullets);
        BulletBox.Visibility = bullets.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        static bool IsBullet(string line) => line.StartsWith("• ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Sets the text with the parts between "**" in bold, eg. "Customer **{0}**".
    /// </summary>
    internal static void SetFormattedText(TextBlock target, string text)
    {
        target.Inlines.Clear();
        if (text.Length == 0) return;

        string[] parts = text.Split("**");
        for (int i = 0; i < parts.Length; i++)
        {
            var run = new Run(parts[i]);
            if (i % 2 == 0)
            {
                target.Inlines.Add(run);
                continue;
            }
            var bold = new Bold(run);
            if (target.TryFindResource("TextBrush") is Brush brush) bold.Foreground = brush;
            target.Inlines.Add(bold);
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
        NoteText.Inlines.Add(new Run(parts[0]));
        NoteText.Inlines.Add(link);
        if (parts.Length > 1) NoteText.Inlines.Add(new Run(parts[1]));
        NoteText.Visibility = Visibility.Visible;
    }

    public static bool ShowConfirm(string message, string? title = null)
    {
        if (IsUnitTest()) return true;
        var dlg = CreateDialog(CustomDialogType.Confirm, title ?? Translations.Dialog_ConfirmTitle, message);
        return dlg.ShowDialog() == true;
    }

    /// <summary>
    /// Confirmation of an action that can't be undone: warning icon, <paramref name="confirmText"/> and cancel.
    /// </summary>
    public static bool ShowWarningConfirm(string message, string title, string confirmText)
    {
        if (IsUnitTest()) return true;
        var dlg = CreateDialog(CustomDialogType.Confirm, title, message);
        dlg.SetIcon(Amber, AmberDark, WarningIcon);
        dlg.OkButton.Content = confirmText;
        dlg.CancelButton.Content = Translations.Dialog_Cancel;
        return dlg.ShowDialog() == true;
    }

    /// <summary>
    /// Asks for a text; <paramref name="placeholder"/> is an example shown in the empty box. Null if the user cancels; in unit tests always null.
    /// </summary>
    public static string? ShowInput(string message, string title, string? errorMessage = null, string? placeholder = null)
    {
        if (IsUnitTest()) return null;
        var dlg = CreateDialog(CustomDialogType.Confirm, title, message);
        dlg.OkButton.Content = Translations.Dialog_OK;
        dlg.OkButton.IsDefault = true;
        dlg.CancelButton.Content = Translations.Dialog_Cancel;
        dlg.InputBox.Visibility = Visibility.Visible;
        dlg.InputPlaceholder.Text = placeholder;
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            dlg.InputErrorText.Text = errorMessage;
            dlg.InputErrorText.Visibility = Visibility.Visible;
        }
        dlg.Loaded += (_, _) => dlg.InputBox.Focus();
        return dlg.ShowDialog() == true ? dlg.InputBox.Text.Trim() : null;
    }

    /// <summary>
    /// Shows one button per option, with the warning icon if <paramref name="warning"/>. The index of the chosen option, or null if the user cancels; in unit tests always null.
    /// </summary>
    public static int? ShowChoice(string message, string title, IReadOnlyList<string> options, bool warning = false)
    {
        if (IsUnitTest()) return null;
        var dlg = CreateDialog(CustomDialogType.Confirm, title, message);
        if (warning) dlg.SetIcon(Amber, AmberDark, WarningIcon);
        dlg.OkButton.Visibility = Visibility.Collapsed;
        dlg.CancelButton.Content = Translations.Dialog_Cancel;
        dlg.CancelButton.Margin = new Thickness(0);
        dlg.ChoicePanel.Visibility = Visibility.Visible;

        int? choice = null;
        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            var button = new System.Windows.Controls.Button
            {
                Content = options[i],
                Height = 36,
                Margin = new Thickness(0, 0, 0, 8),
                Style = (Style)dlg.FindResource("ModernButtonStyle")
            };
            button.Click += (_, _) =>
            {
                choice = index;
                dlg.DialogResult = true;
                dlg.Close();
            };
            dlg.ChoicePanel.Children.Add(button);
        }
        return dlg.ShowDialog() == true ? choice : null;
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
