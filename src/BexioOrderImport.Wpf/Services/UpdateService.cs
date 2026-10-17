using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace BexioOrderImport.Wpf.Services;

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly IAppLifecycleService _appLifecycleService;
    private readonly string _cacheFolder;
    private readonly string _downloadFolder;
    private const string RepoOwner = "nilsthomann";
    private const string RepoName = "bexio-order-import";

    public UpdateService(IHttpClientFactory httpClientFactory, IAppLifecycleService appLifecycleService)
        : this(httpClientFactory?.CreateClient("GitHubUpdate"), appLifecycleService) { }

    internal UpdateService(IAppLifecycleService appLifecycleService)
        : this(new HttpClient(), appLifecycleService) { }

    /// <param name="cacheFolder">Folder of the last check time; tests pass a temp folder instead of %LocalAppData%\BexioOrderImport.</param>
    /// <param name="downloadFolder">Folder of the downloaded installer; tests pass a temp folder instead of %TEMP%\BexioOrderImportUpdate.</param>
    internal UpdateService(HttpClient? httpClient, IAppLifecycleService appLifecycleService, string? cacheFolder = null, string? downloadFolder = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _appLifecycleService = appLifecycleService ?? throw new ArgumentNullException(nameof(appLifecycleService));
        _cacheFolder = cacheFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        _downloadFolder = downloadFolder ?? Path.Combine(Path.GetTempPath(), "BexioOrderImportUpdate");
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BexioOrderImporter", "1.0"));
        }
    }

    private static bool IsUpdateCheckCached(string cacheFilePath)
    {
        if (!File.Exists(cacheFilePath)) return false;
        try
        {
            string text = File.ReadAllText(cacheFilePath).Trim();
            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lastCheckTicks))
            {
                var lastCheck = new DateTime(lastCheckTicks, DateTimeKind.Utc);
                return DateTime.UtcNow - lastCheck < TimeSpan.FromHours(4);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            // The cache only saves API calls; without it the check runs again
            Trace.TraceWarning($"Could not read the update check cache: {ex.Message}");
        }
        return false;
    }

    private static void UpdateLastCheckCache(string appDataFolder, string cacheFilePath)
    {
        try
        {
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            File.WriteAllText(cacheFilePath, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not write the update check cache: {ex.Message}");
        }
    }

    private static string? FindInstallerDownloadUrl(JsonElement root)
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
        string cacheFilePath = Path.Combine(_cacheFolder, "last_update_check.txt");

        if (IsUpdateCheckCached(cacheFilePath))
        {
            return null;
        }

        UpdateLastCheckCache(_cacheFolder, cacheFilePath);

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

    public async Task DownloadAndInstallUpdateAsync(string downloadUrl, Action<double> progressCallback)
    {
        Directory.CreateDirectory(_downloadFolder);
        string tempFilePath = Path.Combine(_downloadFolder, "BexioOrderImportSetup.exe");

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

                while ((bytesRead = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
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
