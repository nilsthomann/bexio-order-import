using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BexioOrderImport.Application.Options;

namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private string? ShowProfileCreateDialogAndValidateName(bool isClone)
    {
        string? name = ProfileCreateDialogProvider(isClone);
        if (name != null)
        {
            if (Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorDialogProvider(Resources.Translations.Dialog_ProfileNameExists, Resources.Translations.Dialog_ErrorTitle);
                return null;
            }
            return name;
        }
        return null;
    }

    private void CreateProfile()
    {
        string? name = ShowProfileCreateDialogAndValidateName(isClone: false);
        if (name != null)
        {
            var newProfile = new Models.MappingProfile
            {
                Name = name,
                Mapping = new ExcelMappingOptions()
            };
            Profiles.Add(newProfile);
            SelectedProfile = newProfile;
            SetModified();

            // Directly open update window
            EditProfile(newProfile);
        }
    }

    private void CloneProfile(Models.MappingProfile profile)
    {
        if (profile == null) return;
        string? name = ShowProfileCreateDialogAndValidateName(isClone: true);
        if (name != null)
        {
            var newProfile = new Models.MappingProfile
            {
                Name = name,
                Mapping = CloneMapping(profile.Mapping)
            };
            Profiles.Add(newProfile);
            SelectedProfile = newProfile;
            SetModified();
        }
    }

    private bool ShowProfileEditDialog(Models.MappingProfile profile)
    {
        return ProfileEditDialogProvider(profile);
    }

    private void EditProfile(Models.MappingProfile profile)
    {
        if (profile == null) return;

        if (ShowProfileEditDialog(profile))
        {
            if (profile == SelectedProfile)
            {
                CopyProfileToVm(profile);
            }
            if (profile == ActiveProfile && !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }
            SetModified();
        }
    }

    private void DeleteProfile(Models.MappingProfile profile)
    {
        if (profile == null || profile.Name == "Default" || Profiles.Count <= 1) return;

        string message = string.Format(Resources.Translations.Confirm_DeleteProfileMessage, profile.Name);
        bool confirmed = ConfirmDialogProvider(message, Resources.Translations.Confirm_DeleteProfileTitle);
        if (!confirmed) return;

        Profiles.Remove(profile);

        if (SelectedProfile == profile)
        {
            SelectedProfile = Profiles[0];
        }
        if (ActiveProfile == profile)
        {
            ActiveProfile = Profiles[0];
        }
        SetModified();
    }

    private void SetActiveProfile(Models.MappingProfile profile)
    {
        if (profile != null)
        {
            ActiveProfile = profile;
            OnPropertyChanged(nameof(ActiveProfile));
            AppendLog($"Active profile set to: {ActiveProfile.Name}");
            SetModified();
        }
    }

    private void ExportProfiles()
    {
        try
        {
            string? fileName = SaveFileDialogProvider("JSON files (*.json)|*.json", ".json", "bexio_mapping_profiles.json");
            if (fileName != null)
            {
                var exportList = Profiles.Select(p => new Models.MappingProfileDto
                {
                    Name = p.Name,
                    ExcelMapping = MapOptionsToDto(p.Mapping)
                }).ToList();

                string json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fileName, json);
                AppendLog($"Profiles exported successfully to: {fileName}");
                InfoDialogProvider(Resources.Translations.Dialog_ExportSuccess);
            }
        }
        catch (Exception ex)
        {
            ErrorDialogProvider($"{Resources.Translations.Settings_ErrorSave}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
    }

    private void ImportProfiles()
    {
        try
        {
            string? fileName = OpenFileDialogProvider("JSON files (*.json)|*.json", ".json");
            if (fileName != null)
            {
                string json = File.ReadAllText(fileName);
                var importedDtos = JsonSerializer.Deserialize<System.Collections.Generic.List<Models.MappingProfileDto>>(json);
                if (importedDtos == null)
                {
                    ErrorDialogProvider(Resources.Translations.Dialog_ImportInvalidFormat, Resources.Translations.Dialog_ErrorTitle);
                    return;
                }

                UpdateProfilesFromImportedDtos(importedDtos, fileName);
            }
        }
        catch (Exception ex)
        {
            ErrorDialogProvider($"{Resources.Translations.Settings_ErrorLoad}: {ex.Message}", Resources.Translations.Settings_ErrorTitle);
        }
    }

    private void UpdateProfilesFromImportedDtos(System.Collections.Generic.List<Models.MappingProfileDto> importedDtos, string fileName)
    {
        bool importedAny = false;
        foreach (var dto in importedDtos)
        {
            if (string.IsNullOrEmpty(dto.Name)) continue;

            ExcelMappingOptions mapping = MapDtoToOptions(dto.ExcelMapping);

            var existing = Profiles.FirstOrDefault(p => p.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Mapping = mapping;
                if (existing == SelectedProfile)
                {
                    CopyProfileToVm(existing);
                }
                if (existing == ActiveProfile && !string.IsNullOrEmpty(SelectedFilePath) && File.Exists(SelectedFilePath))
                {
                    _ = LoadExcelFileAsync(SelectedFilePath);
                }
            }
            else
            {
                Profiles.Add(new Models.MappingProfile { Name = dto.Name, Mapping = mapping });
            }
            importedAny = true;
        }

        if (importedAny)
        {
            SetModified();
            AppendLog($"Profiles imported successfully from: {fileName}");
            InfoDialogProvider(Resources.Translations.Dialog_ImportSuccess);
        }
    }

    private ExcelMappingOptions CloneMapping(ExcelMappingOptions source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ExcelMappingOptions>(json) ?? new ExcelMappingOptions();
    }
}
