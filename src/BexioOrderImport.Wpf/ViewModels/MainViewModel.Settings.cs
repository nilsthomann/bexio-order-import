using System.IO;
using System.Text.Json;
using BexioOrderImport.Format.Peakmile;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private void EnsureAppSettingsFile()
    {
        string? dir = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(_configFilePath))
        {
            // Check if there is an appsettings.json in the application directory to use as a template
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(templatePath))
            {
                try
                {
                    File.Copy(templatePath, _configFilePath, true);
                    return;
                }
                catch (Exception ex)
                {
                    AppendLog($"⚠️ Could not copy appsettings.json template: {ex.Message}");
                }
            }

            var defaultSettings = new Models.AppSettingsDto
            {
                // Without the explicit null the default of 1 for older settings would preselect a payment type.
                Bexio = new Models.BexioSettingsDto { PaymentTypeId = null },
                ActiveProfileName = "Default",
                Profiles =
                [
                    new() { Name = "Default", Format = PeakmileFormatAdapter.Id }
                ]
            };
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(defaultSettings, IndentedJson));
        }
    }

    internal void LoadSettings()
    {
        try
        {
            EnsureAppSettingsFile();
            string text = File.ReadAllText(_configFilePath);
            var dto = JsonSerializer.Deserialize<Models.AppSettingsDto>(text) ?? new Models.AppSettingsDto();

            var encryptedToken = dto.Bexio.ApiToken;
            BexioToken = _encryptionService.Decrypt(encryptedToken);
            if (string.IsNullOrEmpty(BexioToken) && !string.IsNullOrEmpty(encryptedToken) && encryptedToken != "bexio_api_token_here")
                BexioToken = encryptedToken;

            AccountId = dto.Bexio.AccountId;
            TaxId = dto.Bexio.TaxId;
            PaymentTypeId = dto.Bexio.PaymentTypeId;
            SelectedLanguage = dto.Bexio.Language;
            _initialLanguage = SelectedLanguage;
            ApplyLanguage(SelectedLanguage);

            Profiles.Clear();
            if (dto.Profiles != null && dto.Profiles.Count > 0)
            {
                foreach (var p in dto.Profiles)
                {
                    Profiles.Add(_profileSerializer.FromDto(p));
                }
            }
            else
            {
                Profiles.Add(_profileSerializer.FromDto(new Models.MappingProfileDto { Name = "Default", Format = PeakmileFormatAdapter.Id }));
            }

            var active = Profiles.FirstOrDefault(p => p.Name.Equals(dto.ActiveProfileName, StringComparison.OrdinalIgnoreCase)) ?? Profiles[0];
            _activeProfile = active;
            SelectedProfile = active;
            OnPropertyChanged(nameof(ActiveProfile));
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorDialog($"{Resources.Translations.Settings_ErrorLoad}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
        IsModified = false;
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    private void SaveSettings()
    {
        try
        {
            string encryptedToken = _encryptionService.Encrypt(BexioToken);

            var settingsObj = new Models.AppSettingsDto
            {
                Bexio = new Models.BexioSettingsDto
                {
                    ApiToken = encryptedToken,
                    AccountId = AccountId,
                    TaxId = TaxId,
                    PaymentTypeId = PaymentTypeId,
                    Language = SelectedLanguage
                },
                ActiveProfileName = ActiveProfile?.Name ?? "Default",
                Profiles = [.. Profiles.Select(_profileSerializer.ToDto)]
            };

            BackupLegacySettings();
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(settingsObj, IndentedJson));

            _ = CheckBexioConnectionAsync();

            if (!string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }

            ApplyLanguage(SelectedLanguage);
            bool languageChanged = SelectedLanguage != _initialLanguage;

            HandleLanguageReload(languageChanged);

            AppendLog("Settings saved successfully and active Excel file reloaded.");
            IsModified = false;
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorDialog($"{Resources.Translations.Settings_ErrorSave}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
    }

    internal void SaveActiveProfile()
    {
        try
        {
            EnsureAppSettingsFile();
            if (File.Exists(_configFilePath))
            {
                string text = File.ReadAllText(_configFilePath);
                var dto = JsonSerializer.Deserialize<Models.AppSettingsDto>(text) ?? new Models.AppSettingsDto();
                dto.ActiveProfileName = ActiveProfile?.Name ?? "Default";
                File.WriteAllText(_configFilePath, JsonSerializer.Serialize(dto, IndentedJson));
            }
        }
        catch (Exception ex)
        {
            AppendLog($"⚠️ Could not save active profile setting: {ex.Message}");
        }
    }

    private void HandleLanguageReload(bool languageChanged)
    {
        if (!languageChanged)
        {
            _dialogService.ShowInfoDialog(Resources.Translations.Dialog_SettingsSaved);
            return;
        }

        bool reload = _dialogService.ShowConfirmDialog(
            Resources.Translations.Settings_ReloadPromptMessage,
            Resources.Translations.Settings_ReloadPromptTitle);

        if (reload)
        {
            _dialogService.RestartMainWindow();
        }
        _initialLanguage = SelectedLanguage;
    }

    /// <summary>
    /// Copies a settings file from before formats existed once, as older versions can't read the new profile layout.
    /// </summary>
    internal void BackupLegacySettings()
    {
        string backupPath = Path.ChangeExtension(_configFilePath, ".v1.bak.json");
        if (File.Exists(backupPath) || !File.Exists(_configFilePath)) return;

        var dto = JsonSerializer.Deserialize<Models.AppSettingsDto>(File.ReadAllText(_configFilePath));
        if (dto?.Profiles?.Any(p => p.Format == null) == true)
        {
            File.Copy(_configFilePath, backupPath);
        }
    }

    private static void ApplyLanguage(string lang)
    {
        Helpers.LanguageHelper.Apply(lang);
    }

}
