using System.Reflection;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Services;
using FluentAssertions;
using WpfApp = System.Windows.Application;

namespace BexioOrderImport.Tests;

[NotInParallel]
public class WpfDispatcherServiceTests
{
    private static void RunWithNullApplication(Action action)
    {
        var field = typeof(WpfApp).GetField("_appInstance", BindingFlags.Static | BindingFlags.NonPublic);
        var previousApp = WpfApp.Current;
        try
        {
            field?.SetValue(null, null);
            action();
        }
        finally
        {
            if (previousApp != null)
            {
                field?.SetValue(null, previousApp);
            }
        }
    }

    [Test]
    public void Invoke_WhenApplicationIsNull_ExecutesActionSynchronouslyOnCallingThread()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();
            bool executed = false;
            int callingThreadId = Environment.CurrentManagedThreadId;
            int executedThreadId = 0;

            service.Invoke(() =>
            {
                executed = true;
                executedThreadId = Environment.CurrentManagedThreadId;
            });

            executed.Should().BeTrue();
            executedThreadId.Should().Be(callingThreadId);
        });
    }

    [Test]
    public void Invoke_WhenApplicationIsNull_PropagatesExceptionsImmediately()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();

            Action act = () => service.Invoke(() => throw new InvalidOperationException("Test error"));

            act.Should().Throw<InvalidOperationException>().WithMessage("Test error");
        });
    }

    [Test]
    public void Invoke_WhenApplicationIsNull_ExecutesInSequence()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();
            var results = new List<int>();

            service.Invoke(() => results.Add(1));
            service.Invoke(() => results.Add(2));
            service.Invoke(() => results.Add(3));

            results.Should().Equal(1, 2, 3);
        });
    }

    [Test]
    public void Invoke_WithActiveApplication_ExecutesActionOnUIThread()
    {
        WpfTestApplication.EnsureInitialized();
        var service = new WpfDispatcherService();
        bool executed = false;
        int executedThreadId = 0;

        service.Invoke(() =>
        {
            executed = true;
            executedThreadId = Environment.CurrentManagedThreadId;
        });

        executed.Should().BeTrue();
        executedThreadId.Should().Be(WpfApp.Current!.Dispatcher.Thread.ManagedThreadId);
    }
}
