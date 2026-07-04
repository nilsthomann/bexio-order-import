using System;
using System.Threading.Tasks;

namespace BexioOrderImport.Wpf.Services;

/// <summary>
/// Abstraction for checking and installing application updates from GitHub Releases.
/// Using an interface enables clean unit testing without network calls or IsUnitTest() hacks.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks GitHub Releases for a newer version than the currently installed one.
    /// Returns <c>null</c> if no update is available or the check cannot be performed.
    /// Results are cached for 4 hours to avoid GitHub API rate limits.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>
    /// Downloads the installer from <paramref name="downloadUrl"/>, reports download progress
    /// via <paramref name="progressCallback"/> (0–100), then launches the installer and shuts
    /// down the application.
    /// </summary>
    Task DownloadAndInstallUpdateAsync(string downloadUrl, Action<double> progressCallback);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="rawTag"/> (e.g. "v1.2.0") represents a version
    /// newer than <paramref name="currentVersion"/>.
    /// </summary>
    bool IsNewerVersion(string rawTag, Version? currentVersion);
}
