using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Views;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class ProfileCreateDialog : Window
{
    public string ProfileName { get; private set; } = string.Empty;

    public string FormatId { get; private set; } = string.Empty;

    /// <param name="fixedFormatId">Format of the cloned profile; shown but not selectable. Null when creating a profile.</param>
    public ProfileCreateDialog(IReadOnlyList<IFormatAdapter> formats, string? fixedFormatId = null)
    {
        InitializeComponent();
        FormatInput.ItemsSource = formats;
        FormatInput.SelectedValue = fixedFormatId ?? (formats.Count > 0 ? formats[0].FormatId : null);
        FormatInput.IsEnabled = fixedFormatId == null;

        if (fixedFormatId != null)
        {
            Title = Translations.Settings_ProfilesCloneTitle;
            TitleTextBlock.Text = Translations.Settings_ProfilesCloneTitle;
            ActionButton.Content = Translations.Settings_ProfilesCloneButton;
        }
        ProfileNameInput.Focus();
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

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        string name = ProfileNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Views.CustomDialog.ShowWarning(Translations.Dialog_ProfileNameRequired);
            return;
        }

        if (FormatInput.SelectedValue is not string formatId)
        {
            return;
        }

        ProfileName = name;
        FormatId = formatId;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
