using System.IO;

namespace BexioOrderImport.Wpf.Helpers;

/// <summary>
/// The folder of the user's settings and the update check cache, kept when the app updates itself.
/// </summary>
public static class AppPaths
{
    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");

    public static string SettingsFile => Path.Combine(DataFolder, "appsettings.json");
}
