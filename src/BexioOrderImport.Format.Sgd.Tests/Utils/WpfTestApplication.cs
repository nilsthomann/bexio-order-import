using System.Windows;
using System.Windows.Threading;
using WpfApp = System.Windows.Application;

namespace BexioOrderImport.Format.Sgd.Tests.Utils;

/// <summary>
/// Runs code on the UI thread of a shared WPF application that has the theme of the host merged.
/// </summary>
public static class WpfTestApplication
{
    private static readonly Lock _lock = new();

    public static void RunOnUiThread(Action action)
    {
        EnsureInitialized();
        WpfApp.Current!.Dispatcher.Invoke(action);
    }

    private static void EnsureInitialized()
    {
        if (WpfApp.Current != null) return;

        lock (_lock)
        {
            if (WpfApp.Current != null) return;

            var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                var app = new WpfApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/BexioOrderImport.Abstractions.Wpf;component/Styles/AppStyles.xaml")
                });
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            ready.Wait();
        }
    }
}
