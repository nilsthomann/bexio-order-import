using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
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
                Bexio = new Models.BexioSettingsDto(),
                ActiveProfileName = "Default",
                Profiles = new System.Collections.Generic.List<Models.MappingProfileDto>
                {
                    new Models.MappingProfileDto { Name = "Default", ExcelMapping = new Application.Options.ExcelMappingOptions() }
                }
            };
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(defaultSettings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    internal void LoadSettings()
    {
        try
        {
            EnsureAppSettingsFile();
            Application.Helpers.ExcelColumnJsonConverter.ResetMigrationTracker();
            string text = File.ReadAllText(_configFilePath);
            var dto = JsonSerializer.Deserialize<Models.AppSettingsDto>(text) ?? new Models.AppSettingsDto();

            var encryptedToken = dto.Bexio.ApiToken;
            BexioToken = _encryptionService.Decrypt(encryptedToken);
            if (string.IsNullOrEmpty(BexioToken) && !string.IsNullOrEmpty(encryptedToken) && encryptedToken != "bexio_api_token_here")
                BexioToken = encryptedToken;

            AccountId = dto.Bexio.AccountId;
            TaxId = dto.Bexio.TaxId;
            SelectedLanguage = dto.Bexio.Language;
            _initialLanguage = SelectedLanguage;
            ApplyLanguage(SelectedLanguage);

            Profiles.Clear();
            if (dto.Profiles != null && dto.Profiles.Count > 0)
            {
                foreach (var p in dto.Profiles)
                {
                    Profiles.Add(new Models.MappingProfile { Name = p.Name, Mapping = p.ExcelMapping });
                }
            }
            else
            {
                Profiles.Add(new Models.MappingProfile { Name = "Default", Mapping = new ExcelMappingOptions() });
            }

            // TODO: Remove legacy numeric column mapping migration in future versions.
            bool legacyMigrationPerformed = Application.Helpers.ExcelColumnJsonConverter.HasPerformedNumericConversion;
            foreach (var profile in Profiles)
            {
                if (Application.Helpers.ExcelColumnHelper.MigrateProfileColumnMappings(profile.Mapping))
                {
                    legacyMigrationPerformed = true;
                }
            }

            var active = Profiles.FirstOrDefault(p => p.Name.Equals(dto.ActiveProfileName, StringComparison.OrdinalIgnoreCase)) ?? Profiles[0];
            _activeProfile = active;
            SelectedProfile = active;
            OnPropertyChanged(nameof(ActiveProfile));

            if (legacyMigrationPerformed)
            {
                SaveSettings();
                return;
            }
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
                    Language = SelectedLanguage
                },
                ActiveProfileName = ActiveProfile?.Name ?? "Default",
                Profiles = Profiles.Select(p => new Models.MappingProfileDto
                {
                    Name = p.Name,
                    ExcelMapping = p.Mapping
                }).ToList()
            };

            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(settingsObj, new JsonSerializerOptions { WriteIndented = true }));

            _ = CheckBexioConnectionAsync();
            OnPropertyChanged(nameof(IsActiveRowDiscountEnabled));

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

    private ExcelMappingOptions BuildMappingOptions()
    {
        return ActiveProfile != null ? ActiveProfile.Mapping : new ExcelMappingOptions();
    }

    private void ApplyLanguage(string lang)
    {
        Helpers.LanguageHelper.Apply(lang);
    }

}
