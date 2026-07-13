using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using BexioOrderImport.Application.Interfaces;
using BexioOrderImport.Infrastructure.Bexio;
using BexioOrderImport.Wpf.Helpers;
using BexioOrderImport.Wpf.Services;
using BexioOrderImport.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BexioOrderImport.Wpf;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Apply saved language culture BEFORE any window is constructed
        // so that {x:Static} XAML bindings resolve with the correct culture.
        try
        {
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BexioOrderImport");
            string configPath = Path.Combine(appDataFolder, "appsettings.json");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            }

            if (File.Exists(configPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("Bexio", out var bexio) &&
                    bexio.TryGetProperty("Language", out var langProp))
                {
                    LanguageHelper.Apply(langProp.GetString() ?? "de");
                }
            }
        }
        catch
        {
            // Silently fall back to default culture if config cannot be read
        }

        // Build DI container
        var services = new ServiceCollection();
        services.AddHttpClient("BexioApi");
        services.AddSingleton<IBexioClientFactory, BexioClientFactory>();
        services.AddSingleton<IEncryptionService, DpapiEncryptionService>();
        services.AddSingleton<IDispatcherService, WpfDispatcherService>();
        services.AddSingleton<IAppLifecycleService, WpfLifecycleService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddTransient<MainViewModel>();
        Services = services.BuildServiceProvider();

        base.OnStartup(e);
    }
}
