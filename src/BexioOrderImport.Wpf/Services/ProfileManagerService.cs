using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using BexioOrderImport.Abstractions.Formats;
using BexioOrderImport.Wpf.Models;

namespace BexioOrderImport.Wpf.Services;

public class ProfileManagerService : IProfileManagerService
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly IDialogService _dialogService;
    private readonly IFormatRegistry _registry;
    private readonly ProfileSerializer _serializer;

    public ProfileManagerService(IDialogService dialogService, IFormatRegistry registry)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _serializer = new ProfileSerializer(registry);
    }

    public ProfileCreateResult? ShowProfileCreateDialogAndValidateName(ObservableCollection<MappingProfile> profiles, string? fixedFormatId)
    {
        var result = _dialogService.ShowProfileCreateDialog(fixedFormatId);
        if (result != null)
        {
            if (profiles.Any(p => p.Name.Equals(result.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _dialogService.ShowErrorDialog(Resources.Translations.Dialog_ProfileNameExists, Resources.Translations.Dialog_ErrorTitle);
                return null;
            }
            return result;
        }
        return null;
    }

    public MappingProfile? CreateProfile(ObservableCollection<MappingProfile> profiles)
    {
        var result = ShowProfileCreateDialogAndValidateName(profiles, fixedFormatId: null);
        var adapter = _registry.Find(result?.FormatId);
        if (result != null && adapter != null)
        {
            var newProfile = new MappingProfile
            {
                Name = result.Name,
                FormatId = adapter.FormatId,
                Settings = adapter.CreateDefaultSettings()
            };
            profiles.Add(newProfile);
            return newProfile;
        }
        return null;
    }

    public MappingProfile? CloneProfile(ObservableCollection<MappingProfile> profiles, MappingProfile sourceProfile)
    {
        if (sourceProfile == null) return null;
        var result = ShowProfileCreateDialogAndValidateName(profiles, sourceProfile.FormatId);
        if (result != null)
        {
            var adapter = _registry.Find(sourceProfile.FormatId);
            var newProfile = new MappingProfile
            {
                Name = result.Name,
                FormatId = sourceProfile.FormatId,
                Settings = adapter != null && sourceProfile.Settings != null ? adapter.CloneSettings(sourceProfile.Settings) : null,
                RawSettings = sourceProfile.RawSettings
            };
            profiles.Add(newProfile);
            return newProfile;
        }
        return null;
    }

    public bool EditProfile(ObservableCollection<MappingProfile> profiles, MappingProfile profile)
    {
        if (profile == null) return false;
        return _dialogService.ShowProfileEditDialog(profile, profiles);
    }

    public bool DeleteProfile(ObservableCollection<MappingProfile> profiles, MappingProfile profile)
    {
        if (profile == null || profiles.Count <= 1) return false;

        string message = string.Format(Resources.Translations.Confirm_DeleteProfileMessage, profile.Name);
        bool confirmed = _dialogService.ShowConfirmDialog(message, Resources.Translations.Confirm_DeleteProfileTitle);
        if (!confirmed) return false;

        profiles.Remove(profile);
        return true;
    }

    public void ExportProfiles(ObservableCollection<MappingProfile> profiles, Action<string> logInfo)
    {
        try
        {
            string? fileName = _dialogService.ShowSaveFileDialog("JSON files (*.json)|*.json", ".json", "bexio_mapping_profiles.json");
            if (fileName != null)
            {
                var exportList = profiles.Select(_serializer.ToDto).ToList();

                string json = JsonSerializer.Serialize(exportList, IndentedJson);
                File.WriteAllText(fileName, json);
                logInfo($"Profiles exported successfully to: {fileName}");
                _dialogService.ShowInfoDialog(Resources.Translations.Dialog_ExportSuccess);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorDialog($"{Resources.Translations.Settings_ErrorSave}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
    }

    public bool ImportProfiles(ObservableCollection<MappingProfile> profiles, Action<string> logInfo)
    {
        try
        {
            string? fileName = _dialogService.ShowOpenFileDialog("JSON files (*.json)|*.json", ".json");
            if (fileName != null)
            {
                string json = File.ReadAllText(fileName);
                var importedDtos = JsonSerializer.Deserialize<System.Collections.Generic.List<MappingProfileDto>>(json);
                if (importedDtos == null)
                {
                    _dialogService.ShowErrorDialog(Resources.Translations.Dialog_ImportInvalidFormat, Resources.Translations.Dialog_ErrorTitle);
                    return false;
                }

                bool importedAny = false;
                foreach (var dto in importedDtos)
                {
                    if (string.IsNullOrEmpty(dto.Name)) continue;

                    var imported = _serializer.FromDto(dto);
                    var existing = profiles.FirstOrDefault(p => p.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        profiles.Add(imported);
                    }
                    else if (existing.FormatId == imported.FormatId && existing.Settings != null && imported.Settings != null)
                    {
                        // Keep the instance, it may be the active or selected profile
                        existing.Settings = imported.Settings;
                    }
                    else
                    {
                        // The format of a profile can't change, so a different format replaces the profile
                        profiles[profiles.IndexOf(existing)] = imported;
                    }
                    importedAny = true;
                }

                if (importedAny)
                {
                    logInfo($"Profiles imported successfully from: {fileName}");
                    _dialogService.ShowInfoDialog(Resources.Translations.Dialog_ImportSuccess);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowErrorDialog($"{Resources.Translations.Settings_ErrorLoad}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
        return false;
    }
}
