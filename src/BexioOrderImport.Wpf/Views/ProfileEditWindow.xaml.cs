using System.Windows;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Abstractions.Wpf;
using BexioOrderImport.Wpf.Models;
using BexioOrderImport.Wpf.Resources;

namespace BexioOrderImport.Wpf.Views;

/// <summary>
/// Edits the profile name and hosts the settings control of the profile's format.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class ProfileEditWindow : Window
{
    private readonly MappingProfile _profile;
    private readonly IEnumerable<MappingProfile>? _existingProfiles;
    private readonly IProfileConfigControl _settingsControl;

    public ProfileEditWindow(MappingProfile profile, IFormatAdapter format, IFormatUiProvider formatUi, IEnumerable<MappingProfile>? existingProfiles = null)
    {
        InitializeComponent();
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(formatUi);
        _existingProfiles = existingProfiles;

        Title = $"{Translations.Settings_ProfilesEditTitle}: {profile.Name}";
        TitleTextBlock.Text = Title;
        ProfileNameInput.Text = profile.Name;
        FormatInput.ItemsSource = new[] { format };
        FormatInput.SelectedIndex = 0;

        // The control edits a copy, so cancelling leaves the profile unchanged
        _settingsControl = formatUi.CreateProfileConfigControl();
        _settingsControl.Load(format.CloneSettings(profile.Settings!));
        FormatSettingsHost.Content = _settingsControl.View;
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string newName = ProfileNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            CustomDialog.ShowWarning(Translations.Dialog_ProfileNameRequired);
            return;
        }

        if (_existingProfiles != null && _existingProfiles.Any(p => p != _profile && p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            CustomDialog.ShowError(Translations.Dialog_ProfileNameExists, Translations.Dialog_ErrorTitle);
            return;
        }

        var errors = _settingsControl.Validate();
        if (errors.Count > 0)
        {
            CustomDialog.ShowWarning(string.Join(Environment.NewLine, errors));
            return;
        }

        try
        {
            var settings = _settingsControl.Save();
            _profile.Name = newName;
            _profile.Settings = settings;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            CustomDialog.ShowError($"{Translations.Settings_ErrorSave}: {ex.Message}", Translations.Settings_ErrorTitle);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
