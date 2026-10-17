using System.Globalization;
using System.Net;
using System.Text;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Services;
using FluentAssertions;

namespace BexioOrderImport.Tests;

/// <summary>
/// Each test uses its own temp folders for the check cache and the installer download,
/// never %LocalAppData%\BexioOrderImport or the shared %TEMP%\BexioOrderImportUpdate.
/// </summary>
public sealed class UpdateServiceTests : IDisposable
{
    private const string ReleaseJson = @"{
        ""tag_name"": ""v99.9.9"",
        ""body"": ""Release 99.9.9"",
        ""assets"": [
            {
                ""name"": ""BexioOrderImportSetup.exe"",
                ""browser_download_url"": ""https://github.com/nilsthomann/bexio-order-import/releases/download/v99.9.9/BexioOrderImportSetup.exe""
            }
        ]
    }";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"BexioOrderImportUpdateTests_{Guid.NewGuid():N}");
    private readonly FakeAppLifecycleService _lifecycleService = new();
    private int _requestCount;

    private string CacheFolder => Path.Combine(_root, "cache");
    private string DownloadFolder => Path.Combine(_root, "download");
    private string CacheFilePath => Path.Combine(CacheFolder, "last_update_check.txt");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (DirectoryNotFoundException) { }
    }

    private sealed class FakeAppLifecycleService : IAppLifecycleService
    {
        public string? InstallerPathPassed { get; set; }
        public void StartInstallerAndExit(string installerPath)
        {
            InstallerPathPassed = installerPath;
        }
    }

    private UpdateService CreateService(Func<HttpResponseMessage> respond)
    {
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                _requestCount++;
                return Task.FromResult(respond());
            }
        };
        return new UpdateService(new HttpClient(handler), _lifecycleService, CacheFolder, DownloadFolder);
    }

    private UpdateService CreateService(HttpStatusCode status, string? json = null)
        => CreateService(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json")
        });

    private void WriteCache(string content)
    {
        Directory.CreateDirectory(CacheFolder);
        File.WriteAllText(CacheFilePath, content);
    }

    [Test]
    [Arguments("v1.1.0", "1.0.0", true)]
    [Arguments("1.1.0", "1.0.0", true)]
    [Arguments("v2.0.0", "1.9.9", true)]
    [Arguments("v1.0.0", "1.0.0", false)]
    [Arguments("1.0.0", "1.0.0", false)]
    [Arguments("v0.9.0", "1.0.0", false)]
    [Arguments("v1.0.0", null, false)]
    [Arguments("invalid-version", "1.0.0", false)]
    public void IsNewerVersion_ShouldCorrectlyCompareVersions(string rawTag, string? currentVersionStr, bool expectedResult)
    {
        Version? currentVersion = currentVersionStr != null ? Version.Parse(currentVersionStr) : null;

        bool result = CreateService(HttpStatusCode.OK).IsNewerVersion(rawTag, currentVersion);

        result.Should().Be(expectedResult);
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenNewerVersionAvailable_ShouldReturnUpdateInfoAndCacheTheCheck()
    {
        var result = await CreateService(HttpStatusCode.OK, ReleaseJson).CheckForUpdatesAsync();

        result.Should().NotBeNull();
        result!.LatestVersion.Should().Be("v99.9.9");
        result.DownloadUrl.Should().Be("https://github.com/nilsthomann/bexio-order-import/releases/download/v99.9.9/BexioOrderImportSetup.exe");
        result.ReleaseNotes.Should().Be("Release 99.9.9");
        File.Exists(CacheFilePath).Should().BeTrue("the folder is created and the check time is written");
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenCheckWithin4Hours_ShouldReturnNullWithoutRequest()
    {
        WriteCache(DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

        var result = await CreateService(HttpStatusCode.OK, ReleaseJson).CheckForUpdatesAsync();

        result.Should().BeNull();
        _requestCount.Should().Be(0);
    }

    [Test]
    [Arguments("corrupt_non_numeric_ticks")]
    [Arguments("-1")]
    public async Task CheckForUpdatesAsync_WithInvalidCacheFile_ShouldIgnoreItAndCheck(string content)
    {
        WriteCache(content);

        var result = await CreateService(HttpStatusCode.OK, ReleaseJson).CheckForUpdatesAsync();

        result.Should().NotBeNull();
        _requestCount.Should().Be(1);
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenHttpError_ShouldReturnNull()
    {
        var result = await CreateService(HttpStatusCode.InternalServerError).CheckForUpdatesAsync();

        result.Should().BeNull();
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenNoMatchingAsset_ShouldReturnNull()
    {
        string json = ReleaseJson.Replace("BexioOrderImportSetup.exe", "some_other_file.txt");

        var result = await CreateService(HttpStatusCode.OK, json).CheckForUpdatesAsync();

        result.Should().BeNull();
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenJsonInvalid_ShouldThrow()
    {
        Func<Task> act = CreateService(HttpStatusCode.OK, "INVALID_JSON_CONTENT").CheckForUpdatesAsync;

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Test]
    public async Task DownloadAndInstallUpdateAsync_WithValidUrl_ShouldDownloadAndReportProgress()
    {
        var service = CreateService(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024 * 100]) };
            response.Content.Headers.ContentLength = 1024 * 100;
            return response;
        });
        double maxProgress = 0;
        int progressCalls = 0;

        await service.DownloadAndInstallUpdateAsync("https://dummyurl.com/setup.exe", progress =>
        {
            progressCalls++;
            maxProgress = Math.Max(maxProgress, progress);
        });

        _lifecycleService.InstallerPathPassed.Should().Be(Path.Combine(DownloadFolder, "BexioOrderImportSetup.exe"));
        new FileInfo(_lifecycleService.InstallerPathPassed!).Length.Should().Be(1024 * 100);
        progressCalls.Should().BeGreaterThan(0);
        maxProgress.Should().BeApproximately(100.0, 0.01);
    }

    [Test]
    public async Task DownloadAndInstallUpdateAsync_WhenAnOldInstallerExists_ShouldReplaceIt()
    {
        Directory.CreateDirectory(DownloadFolder);
        string installerPath = Path.Combine(DownloadFolder, "BexioOrderImportSetup.exe");
        File.WriteAllText(installerPath, "an older and longer installer");
        var service = CreateService(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[10]) });

        await service.DownloadAndInstallUpdateAsync("https://dummyurl.com/setup.exe", _ => { });

        new FileInfo(installerPath).Length.Should().Be(10);
    }

    [Test]
    public async Task DownloadAndInstallUpdateAsync_WhenHttpError_ShouldThrowException()
    {
        var service = CreateService(HttpStatusCode.BadRequest);

        Func<Task> act = () => service.DownloadAndInstallUpdateAsync("https://dummyurl.com/setup.exe", _ => { });

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Response status code does not indicate success: 400 (Bad Request).");
        _lifecycleService.InstallerPathPassed.Should().BeNull();
    }

    [Test]
    public void Constructors_ShouldInitializeCorrectly()
    {
        var service1 = new UpdateService(_lifecycleService);
        service1.Should().NotBeNull();

        var factoryMock = new Moq.Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("GitHubUpdate")).Returns(new HttpClient());
        var service2 = new UpdateService(factoryMock.Object, _lifecycleService);
        service2.Should().NotBeNull();

        ((Action)(() => _ = new UpdateService((HttpClient?)null, null!))).Should().Throw<ArgumentNullException>();
    }
}
