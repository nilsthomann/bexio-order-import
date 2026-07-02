using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace BexioOrderImport.Wpf.Services;

public class UpdateInfo
{
    public string LatestVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
}

public class UpdateService
{
    private readonly HttpClient _httpClient;
    private const string RepoOwner = "nilsthomann";
    private const string RepoName = "bexio-order-import";

    public UpdateService()
    {
        _httpClient = new HttpClient();
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BexioOrderImporter", "1.0"));
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagProp)) return null;
            string rawTag = tagProp.GetString() ?? "";

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (!IsNewerVersion(rawTag, currentVersion)) return null;

            // Find the installer asset
            string downloadUrl = string.Empty;
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameProp))
                    {
                        string name = nameProp.GetString() ?? "";
                        if (name.Equals("BexioOrderImportSetup.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl)) return null;

            string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

            return new UpdateInfo
            {
                LatestVersion = rawTag,
                DownloadUrl = downloadUrl,
                ReleaseNotes = body
            };
        }
        catch
        {
            // Fail silently, auto-updates shouldn't crash the app startup
            return null;
        }
    }

    public async Task DownloadAndInstallUpdateAsync(string downloadUrl, Action<double> progressCallback)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "BexioOrderImportUpdate");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        string tempFilePath = Path.Combine(tempDir, "BexioOrderImportSetup.exe");

        // Delete old installer if exists
        if (File.Exists(tempFilePath))
        {
            try { File.Delete(tempFilePath); } catch { }
        }

        // Download file with progress reporting
        using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (totalBytes.HasValue)
                    {
                        double progress = (double)totalRead / totalBytes.Value * 100;
                        progressCallback(progress);
                    }
                }
            }
        }

        // Run installer silently and shut down the app
        var psi = new ProcessStartInfo
        {
            FileName = tempFilePath,
            Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true
        };

        Process.Start(psi);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }

    public bool IsNewerVersion(string rawTag, Version? currentVersion)
    {
        if (currentVersion == null) return false;
        string tag = rawTag.TrimStart('v');
        if (Version.TryParse(tag, out var latestVersion))
        {
            return latestVersion > currentVersion;
        }
        return false;
    }
}
