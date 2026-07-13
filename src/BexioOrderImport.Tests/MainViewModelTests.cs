using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Wpf.Services;
using BexioOrderImport.Wpf.ViewModels;
using FluentAssertions;
using Moq;

namespace BexioOrderImport.Tests;

public class MainViewModelTests : IDisposable
{
    private readonly Mock<IUpdateService> _updateServiceMock;
    private readonly Mock<IBexioClientFactory> _clientFactoryMock;
    private readonly Mock<IBexioClient> _clientMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IDispatcherService> _dispatcherServiceMock;
    private readonly Mock<IEncryptionService> _encryptionServiceMock;
    private readonly string _tempFilePath;

    public MainViewModelTests()
    {
        // Initialize WPF Application context for unit tests to prevent null refs on App.Current
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application();
        }

        _tempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString() + "_appsettings.json");

        _updateServiceMock = new Mock<IUpdateService>();
        _clientMock = new Mock<IBexioClient>();
        _clientFactoryMock = new Mock<IBexioClientFactory>();
        _clientFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>())).Returns(_clientMock.Object);
        _dialogServiceMock = new Mock<IDialogService>();
        _dispatcherServiceMock = new Mock<IDispatcherService>();
        // Mock dispatcher service to execute actions immediately
        _dispatcherServiceMock.Setup(d => d.Invoke(It.IsAny<Action>())).Callback<Action>(a => a());
        _dispatcherServiceMock.Setup(d => d.BeginInvoke(It.IsAny<Action>())).Callback<Action>(a => a());
        _encryptionServiceMock = new Mock<IEncryptionService>();
        _encryptionServiceMock.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => s);
        _encryptionServiceMock.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s);
    }

    public void Dispose()
    {
        try
        {
            if (System.IO.File.Exists(_tempFilePath))
            {
                System.IO.File.Delete(_tempFilePath);
            }
        }
        catch { }
    }

    private MainViewModel CreateVm()
    {
        return new MainViewModel(
            _updateServiceMock.Object,
            _clientFactoryMock.Object,
            _dialogServiceMock.Object,
            _dispatcherServiceMock.Object,
            _encryptionServiceMock.Object,
            _tempFilePath);
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

    [Fact]
    public void ImportCommand_WhenAccountOrTaxIdNull_ShouldShowErrorDialogAndAbort()
    {
        // Arrange
        var vm = CreateVm();
        vm.AccountId = null; // null triggers error
        vm.TaxId = 1;
        
        // Simulating a loaded order so the command can execute
        typeof(MainViewModel).GetField("_loadedOrder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(vm, new Order { Customer = new Customer { CompanyName = "Test customer" } });
        
        vm.ImportCommand.RaiseCanExecuteChanged();

        // Act
        vm.ImportCommand.Execute(null);

        // Assert
        _dialogServiceMock.Verify(d => d.ShowErrorDialog(
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Once);
        vm.IsImporting.Should().BeFalse();
    }

    [Fact]
    public async Task CheckBexioConnectionAsync_ShouldTriggerConnectionCheckAndPopulateLists()
    {
        // Arrange
        var vm = CreateVm();
        vm.BexioToken = "some-token";
        
        var accounts = new List<BexioAccount>
        {
            new BexioAccount { Id = 100, AccountNo = "1000", Name = "Cash Account", IsActive = true }
        };
        var taxes = new List<BexioTax>
        {
            new BexioTax { Id = 5, DisplayName = "MwSt 8.1%", Percentage = 8.1m, IsActive = true }
        };
        
        _clientMock.Setup(c => c.CheckConnectionAsync()).ReturnsAsync(true);
        _clientMock.Setup(c => c.GetAccountsAsync()).ReturnsAsync(accounts);
        _clientMock.Setup(c => c.GetTaxesAsync()).ReturnsAsync(taxes);

        // Act
        await vm.CheckBexioConnectionAsync();

        // Assert
        vm.IsConnectionSuccessful.Should().BeTrue();
        vm.ConnectionStatusColor.Should().Be("#10B981"); // Green success
        vm.AccountsList.Count.Should().Be(1);
        vm.AccountsList[0].Id.Should().Be(100);
        vm.TaxesList.Count.Should().Be(1);
        vm.TaxesList[0].Id.Should().Be(5);
    }
}
