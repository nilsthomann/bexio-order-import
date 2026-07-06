namespace BexioOrderImport.Wpf.Services;

/// <summary>
/// Holds information about an available application update.
/// </summary>
public class UpdateInfo
{
    /// <summary>The latest version tag, e.g. "v1.2.0".</summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>Direct download URL for the installer asset.</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Release notes / changelog from the GitHub release body.</summary>
    public string ReleaseNotes { get; set; } = string.Empty;
}
