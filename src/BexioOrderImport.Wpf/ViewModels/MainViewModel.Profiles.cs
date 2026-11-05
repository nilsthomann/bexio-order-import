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
        if (profile.Settings == null)
        {
            _dialogService.ShowErrorDialog(string.Format(Resources.Translations.Error_FormatUnavailable, profile.FormatId), Resources.Translations.Dialog_ErrorTitle);
            return;
        }
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

    private int _dragStartIndex;

    /// <summary>
    /// The profile that is dragged in the profile list; its row shows a placeholder at the position it would land.
    /// </summary>
    public Models.MappingProfile? DraggedProfile
    {
        get => _draggedProfile;
        private set => SetProperty(ref _draggedProfile, value);
    }
    private Models.MappingProfile? _draggedProfile;

    internal void BeginProfileDrag(Models.MappingProfile profile)
    {
        _dragStartIndex = Profiles.IndexOf(profile);
        DraggedProfile = _dragStartIndex >= 0 ? profile : null;
    }

    /// <summary>
    /// Moves the dragged profile to the position of the profile under the mouse while dragging.
    /// The list order is the order in the settings file and in the import tab dropdown.
    /// </summary>
    internal void MoveDraggedProfile(int newIndex)
    {
        if (DraggedProfile == null) return;
        int oldIndex = Profiles.IndexOf(DraggedProfile);
        newIndex = Math.Clamp(newIndex, 0, Profiles.Count - 1);
        if (oldIndex != newIndex) Profiles.Move(oldIndex, newIndex);
    }

    /// <summary>
    /// Keeps the new order when the profile was dropped on the list, otherwise moves it back.
    /// </summary>
    internal void EndProfileDrag(bool dropped)
    {
        if (DraggedProfile == null) return;
        int index = Profiles.IndexOf(DraggedProfile);
        if (!dropped)
        {
            Profiles.Move(index, _dragStartIndex);
        }
        else if (index != _dragStartIndex)
        {
            SetModified();
        }
        DraggedProfile = null;
    }

    private void SetActiveProfile(Models.MappingProfile profile)
    {
        if (profile != null)
        {
            bool changed = profile != ActiveProfile;
            AppendLog($"Active profile set to: {profile.Name}");
            // Another profile reads the loaded file again, see ActiveProfile
            ActiveProfile = profile;
            NotifyActiveProfileChanged();
            SaveActiveProfile();
            if (!changed)
            {
                // Selecting the active profile again refreshes the loaded file
                ReloadLoadedFile();
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
            // An imported profile with another format replaces the instance, follow it by name
            if (ActiveProfile != null && !Profiles.Contains(ActiveProfile))
            {
                ActiveProfile = Profiles.FirstOrDefault(p => p.Name == ActiveProfile.Name) ?? Profiles[0];
            }
            if (SelectedProfile != null && !Profiles.Contains(SelectedProfile))
            {
                SelectedProfile = Profiles.FirstOrDefault(p => p.Name == SelectedProfile.Name) ?? Profiles[0];
            }
            SetModified();
        }
    }
}
