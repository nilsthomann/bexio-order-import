using System.Collections.ObjectModel;
using BexioOrderImport.Wpf.Models;

namespace BexioOrderImport.Wpf.Services;

public interface IProfileManagerService
{
    string? ShowProfileCreateDialogAndValidateName(ObservableCollection<MappingProfile> profiles, bool isClone);
    MappingProfile? CreateProfile(ObservableCollection<MappingProfile> profiles);
    MappingProfile? CloneProfile(ObservableCollection<MappingProfile> profiles, MappingProfile sourceProfile);
    bool EditProfile(ObservableCollection<MappingProfile> profiles, MappingProfile profile);
    bool DeleteProfile(ObservableCollection<MappingProfile> profiles, MappingProfile profile);
    void ExportProfiles(ObservableCollection<MappingProfile> profiles, Action<string> logInfo);
    bool ImportProfiles(ObservableCollection<MappingProfile> profiles, Action<string> logInfo);
}
