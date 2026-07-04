using System;
using Xunit;
using FluentAssertions;
using BexioOrderImport.Wpf.Services;

namespace BexioOrderImport.Tests;

public class UpdateServiceTests
{
    private readonly UpdateService _updateService;

    public UpdateServiceTests()
    {
        _updateService = new UpdateService();
    }

    [Theory]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v0.9.0", "1.0.0", false)]
    [InlineData("v1.0.0", null, false)]
    [InlineData("invalid-version", "1.0.0", false)]
    public void IsNewerVersion_ShouldCorrectlyCompareVersions(string rawTag, string? currentVersionStr, bool expectedResult)
    {
        // Arrange
        Version? currentVersion = currentVersionStr != null ? Version.Parse(currentVersionStr) : null;

        // Act
        bool result = _updateService.IsNewerVersion(rawTag, currentVersion);

        // Assert
        result.Should().Be(expectedResult);
    }

    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        public Func<System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken, Task<System.Net.Http.HttpResponseMessage>> SendAsyncFunc { get; set; } = null!;

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            return SendAsyncFunc(request, cancellationToken);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerVersionAvailable_ShouldReturnUpdateInfo()
    {
        // Delete last check cache if it exists, to ensure check runs
        string appDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        string cacheFilePath = System.IO.Path.Combine(appDataFolder, "last_update_check.txt");
        if (System.IO.File.Exists(cacheFilePath))
        {
            try { System.IO.File.Delete(cacheFilePath); } catch {}
        }

        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(@"{
                        ""tag_name"": ""v99.9.9"",
                        ""body"": ""Release 99.9.9"",
                        ""assets"": [
                            {
                                ""name"": ""BexioOrderImportSetup.exe"",
                                ""browser_download_url"": ""https://github.com/nils-thomann/bexio-order-import/releases/download/v99.9.9/BexioOrderImportSetup.exe""
                            }
                        ]
                    }", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new System.Net.Http.HttpClient(handler);
        var updateService = new UpdateService(httpClient);

        // Act
        var result = await updateService.CheckForUpdatesAsync();

        // Assert
        result.Should().NotBeNull();
        result!.LatestVersion.Should().Be("v99.9.9");
        result.DownloadUrl.Should().Be("https://github.com/nils-thomann/bexio-order-import/releases/download/v99.9.9/BexioOrderImportSetup.exe");
        result.ReleaseNotes.Should().Be("Release 99.9.9");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenCheckWithin4Hours_ShouldReturnNull()
    {
        // Arrange
        string appDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        if (!System.IO.Directory.Exists(appDataFolder))
        {
            System.IO.Directory.CreateDirectory(appDataFolder);
        }
        string cacheFilePath = System.IO.Path.Combine(appDataFolder, "last_update_check.txt");
        System.IO.File.WriteAllText(cacheFilePath, DateTime.UtcNow.Ticks.ToString());

        var updateService = new UpdateService();

        // Act
        var result = await updateService.CheckForUpdatesAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenHttpError_ShouldReturnNull()
    {
        // Arrange
        string appDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        string cacheFilePath = System.IO.Path.Combine(appDataFolder, "last_update_check.txt");
        if (System.IO.File.Exists(cacheFilePath))
        {
            try { System.IO.File.Delete(cacheFilePath); } catch {}
        }

        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) => Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError))
        };

        var httpClient = new System.Net.Http.HttpClient(handler);
        var updateService = new UpdateService(httpClient);

        // Act
        var result = await updateService.CheckForUpdatesAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoMatchingAsset_ShouldReturnNull()
    {
        // Arrange
        string appDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BexioOrderImport");
        string cacheFilePath = System.IO.Path.Combine(appDataFolder, "last_update_check.txt");
        if (System.IO.File.Exists(cacheFilePath))
        {
            try { System.IO.File.Delete(cacheFilePath); } catch {}
        }

        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(@"{
                        ""tag_name"": ""v99.9.9"",
                        ""assets"": [
                            {
                                ""name"": ""some_other_file.txt"",
                                ""browser_download_url"": ""https://github.com/nils-thomann/bexio-order-import/releases/download/v99.9.9/some_other_file.txt""
                            }
                        ]
                    }", System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        };

        var httpClient = new System.Net.Http.HttpClient(handler);
        var updateService = new UpdateService(httpClient);

        // Act
        var result = await updateService.CheckForUpdatesAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAndInstallUpdateAsync_WithValidUrl_ShouldDownloadAndReportProgress()
    {
        // Arrange
        var handler = new MockHttpMessageHandler
        {
            SendAsyncFunc = (req, token) =>
            {
                var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.ByteArrayContent(new byte[1024 * 100]) // 100 KB
                };
                response.Content.Headers.ContentLength = 1024 * 100;
                return Task.FromResult(response);
            }
        };

        var httpClient = new System.Net.Http.HttpClient(handler);
        var updateService = new UpdateService(httpClient);
        
        string? passedPath = null;
        updateService.ProcessStartAndShutdownTestHook = path => passedPath = path;

        double maxProgress = 0;
        int progressCalls = 0;

        // Act
        await updateService.DownloadAndInstallUpdateAsync("https://dummyurl.com/setup.exe", progress =>
        {
            progressCalls++;
            if (progress > maxProgress) maxProgress = progress;
        });

        // Assert
        passedPath.Should().NotBeNull();
        System.IO.File.Exists(passedPath!).Should().BeTrue();
        progressCalls.Should().BeGreaterThan(0);
        maxProgress.Should().BeApproximately(100.0, 0.01);

        // Cleanup
        try { System.IO.File.Delete(passedPath!); } catch { }
    }
}
