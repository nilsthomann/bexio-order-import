using System.Threading;
using System.Windows.Threading;
using WpfApp = System.Windows.Application;

namespace BexioOrderImport.Tests.Utils;

public static class WpfTestApplication
{
    private static readonly object _lock = new();

    public static void EnsureInitialized()
    {
        if (WpfApp.Current != null) return;

        lock (_lock)
        {
            if (WpfApp.Current != null) return;

            var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                if (WpfApp.Current == null)
                {
                    _ = new WpfApp();
                }
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

