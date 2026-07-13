using BexioOrderImport.Wpf.Services;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class WpfDispatcherServiceTests
{
    [Fact]
    public void Invoke_WhenApplicationNull_ShouldExecuteActionImmediately()
    {
        // Arrange
        var oldApp = System.Windows.Application.Current;
        SetApplicationCurrent(null);

        try
        {
            var service = new WpfDispatcherService();
            bool executed = false;

            // Act
            service.Invoke(() => executed = true);

            // Assert
            executed.Should().BeTrue();
        }
        finally
        {
            SetApplicationCurrent(oldApp);
        }
    }

    [Fact]
    public void BeginInvoke_WhenApplicationNull_ShouldExecuteActionImmediately()
    {
        // Arrange
        var oldApp = System.Windows.Application.Current;
        SetApplicationCurrent(null);

        try
        {
            var service = new WpfDispatcherService();
            bool executed = false;

            // Act
            service.BeginInvoke(() => executed = true);

            // Assert
            executed.Should().BeTrue();
        }
        finally
        {
            SetApplicationCurrent(oldApp);
        }
    }

    [Fact]
    public void BeginInvoke_WhenApplicationNotNull_ShouldPostToDispatcher()
    {
        // Arrange
        if (System.Windows.Application.Current == null)
        {
            new System.Windows.Application();
        }

        var service = new WpfDispatcherService();
        bool executed = false;

        // Act
        service.BeginInvoke(() => executed = true);

        // Process dispatcher queue on the thread owning Application.Current
        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        // Assert
        executed.Should().BeTrue();
    }

    private void SetApplicationCurrent(System.Windows.Application? app)
    {
        typeof(System.Windows.Application)
            .GetField("_appInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(null, app);
    }
}
