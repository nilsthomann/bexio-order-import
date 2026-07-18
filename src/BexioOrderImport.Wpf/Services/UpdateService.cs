using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace BexioOrderImport.Wpf.Services;

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly IAppLifecycleService _appLifecycleService;
    private const string RepoOwner = "nils-thomann";
    private const string RepoName = "bexio-order-import";

    public UpdateService(IAppLifecycleService appLifecycleService) : this(new HttpClient(), appLifecycleService) { }

    public UpdateService(HttpClient httpClient, IAppLifecycleService appLifecycleService)
    {
        _httpClient = httpClient;
        _appLifecycleService = appLifecycleService;
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BexioOrderImporter", "1.0"));
    }

    private bool IsUpdateCheckCached(string cacheFilePath)
    {
        if (!File.Exists(cacheFilePath)) return false;
        try
        {
            string text = File.ReadAllText(cacheFilePath).Trim();
            if (long.TryParse(text, out long lastCheckTicks))
            {
                var lastCheck = new DateTime(lastCheckTicks, DateTimeKind.Utc);
                return DateTime.UtcNow - lastCheck < TimeSpan.FromHours(4);
            }
        }
        catch { }
        return false;
    }

    private void UpdateLastCheckCache(string appDataFolder, string cacheFilePath)
    {
        try
        {
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            File.WriteAllText(cacheFilePath, DateTime.UtcNow.Ticks.ToString());
        }
        catch { }
    }

    private string? FindInstallerDownloadUrl(JsonElement root)
    {
        if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsProp.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var nameProp))
                {
                    string name = nameProp.GetString() ?? string.Empty;
                    if (name.Equals("BexioOrderImportSetup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return asset.GetProperty("browser_download_url").GetString();
                    }
                }
            }
        }
        return null;
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        string cacheFilePath = Path.Combine(appDataFolder, "last_update_check.txt");
        
        if (IsUpdateCheckCached(cacheFilePath))
        {
            return null;
        }

        try
        {
            UpdateLastCheckCache(appDataFolder, cacheFilePath);

            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagProp)) return null;
            string rawTag = tagProp.GetString() ?? string.Empty;

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (!IsNewerVersion(rawTag, currentVersion)) return null;

            string? downloadUrl = FindInstallerDownloadUrl(root);
            if (string.IsNullOrEmpty(downloadUrl)) return null;

            string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : "";

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

        _appLifecycleService.StartInstallerAndExit(tempFilePath);
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
