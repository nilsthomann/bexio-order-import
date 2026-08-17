namespace BexioOrderImport.Wpf.ViewModels;

public partial class MainViewModel
{
    private void CreateProfile()
    {
        var newProfile = _profileManagerService.CreateProfile(Profiles);
        if (newProfile != null)
        {
            SelectedProfile = newProfile;
            SetModified();
            EditProfile(newProfile);
        }
    }

    private void CloneProfile(Models.MappingProfile profile)
    {
        if (profile == null) return;
        var newProfile = _profileManagerService.CloneProfile(Profiles, profile);
        if (newProfile != null)
        {
            SelectedProfile = newProfile;
            SetModified();
        }
    }

    private void EditProfile(Models.MappingProfile profile)
    {
        if (profile == null) return;
        if (_profileManagerService.EditProfile(Profiles, profile))
        {
            NotifyActiveProfileChanged();
            SetModified();
        }
    }

    private void DeleteProfile(Models.MappingProfile profile)
    {
        if (profile == null || Profiles.Count <= 1) return;
        if (_profileManagerService.DeleteProfile(Profiles, profile))
        {
            if (SelectedProfile == profile)
            {
                SelectedProfile = Profiles[0];
            }
            if (ActiveProfile == profile)
            {
                ActiveProfile = Profiles[0];
            }
            NotifyActiveProfileChanged();
            SetModified();
        }
    }

    private void SetActiveProfile(Models.MappingProfile profile)
    {
        if (profile != null)
        {
            ActiveProfile = profile;
            NotifyActiveProfileChanged();
            AppendLog($"Active profile set to: {ActiveProfile.Name}");
            SaveActiveProfile();
            if (!string.IsNullOrEmpty(SelectedFilePath) && System.IO.File.Exists(SelectedFilePath))
            {
                _ = LoadExcelFileAsync(SelectedFilePath);
            }
        }
    }

    private void ExportProfiles()
    {
        _profileManagerService.ExportProfiles(Profiles, message => AppendLog(message));
    }

    private void ImportProfiles()
    {
        if (_profileManagerService.ImportProfiles(Profiles, message => AppendLog(message)))
        {
            SetModified();
        }
    }
}
