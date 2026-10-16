using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Wpf.Models;

namespace BexioOrderImport.Tests.Utils;

internal static class ProfileTestExtensions
{
    public static MappingProfile PeakmileProfile(string name, PeakmileSettings? settings = null) => new()
    {
        Name = name,
        FormatId = PeakmileFormatAdapter.Id,
        Settings = settings ?? new PeakmileSettings()
    };

    public static PeakmileSettings Peakmile(this MappingProfile profile) => (PeakmileSettings)profile.Settings!;
}
