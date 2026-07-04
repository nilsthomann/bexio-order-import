using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using FluentAssertions;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Services;
using BexioOrderImport.Wpf.ViewModels;

namespace BexioOrderImport.Tests;

public class MainViewModelTests
{
    private static MainViewModel CreateVm()
        => new MainViewModel(new MockUpdateService(), new MockBexioClientFactory());

    public MainViewModelTests()
    {
        // Initialize WPF Application context for unit tests to prevent null refs on App.Current
        if (System.Windows.Application.Current == null)
        {
            new System.Windows.Application();
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var vm = CreateVm();

        // Assert
        vm.IsImporting.Should().BeFalse();
        vm.IsLoading.Should().BeFalse();
        vm.IsUpdateAvailable.Should().BeFalse();
        vm.AppVersion.Should().StartWith("v");
        vm.HasLoadedFile.Should().BeFalse();
    }

    [Fact]
    public void SetFileProperties_ShouldUpdateStateCorrectly()
    {
        // Arrange
        var vm = CreateVm();

        // Act
        vm.SelectedFilePath = "C:\\test\\order.xlsx";
        vm.SelectedFileName = "order.xlsx";
        vm.FileSizeText = "12 KB";
        vm.HasLoadedFile = true;

        // Assert
        vm.SelectedFilePath.Should().Be("C:\\test\\order.xlsx");
        vm.SelectedFileName.Should().Be("order.xlsx");
        vm.FileSizeText.Should().Be("12 KB");
        vm.HasLoadedFile.Should().BeTrue();
    }

    [Fact]
    public void SetLanguage_ShouldUpdateSelectedLanguage()
    {
        // Arrange
        var vm = CreateVm();

        // Act
        vm.SelectedLanguage = "en";

        // Assert
        vm.SelectedLanguage.Should().Be("en");
    }

    [Fact]
    public void BexioTokenDisplay_WhenNotFocused_ShouldReturnDots()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "my-secret-token";
        vm.IsTokenFocused = false;

        // Act & Assert
        vm.BexioTokenDisplay.Should().Be(new string('•', 24));
    }

    [Fact]
    public void BexioTokenDisplay_WhenFocused_ShouldReturnRealToken()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "my-secret-token";
        vm.IsTokenFocused = true;

        // Act & Assert
        vm.BexioTokenDisplay.Should().Be("my-secret-token");
    }

    [Fact]
    public void BexioTokenDisplay_WhenNotFocusedAndEmpty_ShouldReturnEmpty()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "";
        vm.IsTokenFocused = false;

        // Act & Assert
        vm.BexioTokenDisplay.Should().Be("");
    }

    [Fact]
    public void BexioTokenDisplay_WhenSetWhileFocused_ShouldUpdateBexioToken()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "old-token";
        vm.IsTokenFocused = true;

        // Act
        vm.BexioTokenDisplay = "new-token";

        // Assert
        vm.BexioToken.Should().Be("new-token");
    }

    [Fact]
    public void BexioTokenDisplay_WhenSetWhileNotFocused_ShouldNotUpdateBexioToken()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "old-token";
        vm.IsTokenFocused = false;

        // Act
        vm.BexioTokenDisplay = "new-token";

        // Assert
        vm.BexioToken.Should().Be("old-token");
    }

    // Mock stubs for DI dependencies
    private class MockUpdateService : IUpdateService
    {
        public Task<UpdateInfo?> CheckForUpdatesAsync() => Task.FromResult<UpdateInfo?>(null);
        public Task DownloadAndInstallUpdateAsync(string downloadUrl, Action<double> progressCallback) => Task.CompletedTask;
        public bool IsNewerVersion(string rawTag, Version? currentVersion) => false;
    }

    private class MockBexioClientFactory : IBexioClientFactory
    {
        public IBexioClient Create(string apiToken, int accountId, int taxId) => new MockBexioClient();
    }

    private class MockBexioClient : IBexioClient
    {
        public Task<int?> FindContactIdAsync(string email) => Task.FromResult<int?>(null);
        public Task<int> CreateContactAsync(Customer customer) => Task.FromResult(0);
        public Task<int> CreateOrderAsync(int contactId, Order order) => Task.FromResult(0);
        public Task<int?> FindArticleIdAsync(string articleNumber, string articleName) => Task.FromResult<int?>(null);
        public Task AddArticlePositionAsync(int orderId, int articleId, OrderPosition position) => Task.CompletedTask;
        public Task AddCustomPositionAsync(int orderId, OrderPosition position) => Task.CompletedTask;
    }
}
