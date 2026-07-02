using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace BexioOrderImport.Wpf;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Apply saved language culture BEFORE any window is constructed
        // so that {x:Static} XAML bindings resolve with the correct culture.
        try
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(configPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("Bexio", out var bexio) &&
                    bexio.TryGetProperty("Language", out var langProp))
                {
                    string lang = langProp.GetString() ?? "de";
                    var culture = new CultureInfo(lang == "en" ? "en-US" : "de-CH");
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = culture;
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
            }
        }
        catch
        {
            // Silently fall back to default culture if config cannot be read
        }

        base.OnStartup(e);
    }
}
