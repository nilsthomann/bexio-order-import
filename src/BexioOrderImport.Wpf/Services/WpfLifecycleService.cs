using System.Diagnostics;
using System.Windows;

namespace BexioOrderImport.Wpf.Services;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class WpfLifecycleService : IAppLifecycleService
{
    public void StartInstallerAndExit(string installerPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true
        };

        Process.Start(psi);
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });
    }
}
