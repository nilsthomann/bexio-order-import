using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Services;
using FluentAssertions;
using Xunit;
using WpfApp = System.Windows.Application;

namespace BexioOrderImport.Tests;

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

    [Fact]
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

    [Fact]
    public void BeginInvoke_WhenApplicationIsNull_ExecutesActionSynchronouslyOnCallingThread()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();
            bool executed = false;
            int callingThreadId = Environment.CurrentManagedThreadId;
            int executedThreadId = 0;

            service.BeginInvoke(() =>
            {
                executed = true;
                executedThreadId = Environment.CurrentManagedThreadId;
            });

            executed.Should().BeTrue();
            executedThreadId.Should().Be(callingThreadId);
        });
    }

    [Fact]
    public void Invoke_WhenApplicationIsNull_PropagatesExceptionsImmediately()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();

            Action act = () => service.Invoke(() => throw new InvalidOperationException("Test error"));

            act.Should().Throw<InvalidOperationException>().WithMessage("Test error");
        });
    }

    [Fact]
    public void BeginInvoke_WhenApplicationIsNull_PropagatesExceptionsImmediately()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();

            Action act = () => service.BeginInvoke(() => throw new InvalidOperationException("Test error"));

            act.Should().Throw<InvalidOperationException>().WithMessage("Test error");
        });
    }

    [Fact]
    public void InvokeAndBeginInvoke_WhenApplicationIsNull_ExecutesInSequence()
    {
        RunWithNullApplication(() =>
        {
            var service = new WpfDispatcherService();
            var results = new List<int>();

            service.Invoke(() => results.Add(1));
            service.BeginInvoke(() => results.Add(2));
            service.Invoke(() => results.Add(3));

            results.Should().Equal(1, 2, 3);
        });
    }

    [Fact]
    public async Task BeginInvoke_ExecutesAction()
    {
        var service = new WpfDispatcherService();
        var tcs = new TaskCompletionSource<bool>();

        service.BeginInvoke(() => tcs.SetResult(true));

        bool executed = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        executed.Should().BeTrue();
    }

    [Fact]
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

    [Fact]
    public async Task BeginInvoke_WithActiveApplication_ExecutesActionOnUIThread()
    {
        WpfTestApplication.EnsureInitialized();
        var service = new WpfDispatcherService();
        var tcs = new TaskCompletionSource<int>();

        service.BeginInvoke(() =>
        {
            tcs.SetResult(Environment.CurrentManagedThreadId);
        });

        int executedThreadId = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        executedThreadId.Should().Be(WpfApp.Current!.Dispatcher.Thread.ManagedThreadId);
    }
}

