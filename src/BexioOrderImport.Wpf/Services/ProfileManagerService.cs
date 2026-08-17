using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using BexioOrderImport.Application.Options;
using BexioOrderImport.Application.Services;
using BexioOrderImport.Wpf.Models;

namespace BexioOrderImport.Wpf.Services;

public class ProfileManagerService : IProfileManagerService
{
    private readonly IDialogService _dialogService;

    public ProfileManagerService(IDialogService dialogService)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public string? ShowProfileCreateDialogAndValidateName(ObservableCollection<MappingProfile> profiles, bool isClone)
    {
        string? name = _dialogService.ShowProfileCreateDialog(isClone);
        if (name != null)
        {
            if (profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _dialogService.ShowErrorDialog(Resources.Translations.Dialog_ProfileNameExists, Resources.Translations.Dialog_ErrorTitle);
                return null;
            }
            return name;
        }
        return null;
    }

    public MappingProfile? CreateProfile(ObservableCollection<MappingProfile> profiles)
    {
        string? name = ShowProfileCreateDialogAndValidateName(profiles, isClone: false);
        if (name != null)
        {
            var newProfile = new MappingProfile
            {
                Name = name,
                Mapping = new ExcelMappingOptions()
            };
            profiles.Add(newProfile);
            return newProfile;
        }
        return null;
    }

    public MappingProfile? CloneProfile(ObservableCollection<MappingProfile> profiles, MappingProfile sourceProfile)
    {
        if (sourceProfile == null) return null;
        string? name = ShowProfileCreateDialogAndValidateName(profiles, isClone: true);
        if (name != null)
        {
            var newProfile = new MappingProfile
            {
                Name = name,
                Mapping = ExcelMappingEvaluator.CloneOptions(sourceProfile.Mapping)
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
                var exportList = profiles.Select(p => new MappingProfileDto
                {
                    Name = p.Name,
                    ExcelMapping = p.Mapping
                }).ToList();

                string json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
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

                    ExcelMappingOptions mapping = dto.ExcelMapping;
                    var existing = profiles.FirstOrDefault(p => p.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Mapping = mapping;
                    }
                    else
                    {
                        profiles.Add(new MappingProfile { Name = dto.Name, Mapping = mapping });
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
