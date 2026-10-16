using System.Windows;
using System.Windows.Controls;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Views;
using BexioOrderImport.Wpf.Views.Controls;
using FluentAssertions;
using WpfApp = System.Windows.Application;

namespace BexioOrderImport.Tests;

/// <summary>
/// Loads the XAML of the host views with the shared theme, so missing resources fail here and not at runtime.
/// Each format tests its own controls in its test project.
/// </summary>
[NotInParallel]
public class FormatControlsLoadTests
{
    private static void RunOnUiThread(Action action)
    {
        WpfTestApplication.EnsureInitialized();
        WpfApp.Current!.Dispatcher.Invoke(() =>
        {
            // Closing the test windows must not shut down the shared test application
            WpfApp.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var themeUri = new Uri("pack://application:,,,/BexioOrderImport.Abstractions.Wpf;component/Styles/AppStyles.xaml");
            var resources = WpfApp.Current.Resources;
            if (!resources.MergedDictionaries.Any(d => d.Source == themeUri))
            {
                resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
            }
            action();
        });
    }

    [Test]
    public void ProfileEditWindow_ShouldHostTheFormatControl()
    {
        RunOnUiThread(() =>
        {
            var profile = ProfileTestExtensions.FakeProfile("Default");

            var window = new ProfileEditWindow(profile, new FakeFormatAdapter(), new FakeUiProvider());

            window.FormatSettingsHost.Content.Should().BeOfType<Border>();
            window.FormatInput.IsEnabled.Should().BeFalse();
            window.FormatInput.SelectedValue.Should().BeOfType<FakeFormatAdapter>();
            window.ProfileNameInput.Text.Should().Be("Default");
            window.Close();
        });
    }

    [Test]
    public void ProfileCreateDialog_WhenCloning_ShouldFixTheFormat()
    {
        RunOnUiThread(() =>
        {
            var formats = new[] { new FakeFormatAdapter() };

            var create = new ProfileCreateDialog(formats);
            var clone = new ProfileCreateDialog(formats, FakeFormatAdapter.Id);

            create.FormatInput.IsEnabled.Should().BeTrue();
            create.FormatInput.SelectedValue.Should().Be(FakeFormatAdapter.Id);
            clone.FormatInput.IsEnabled.Should().BeFalse();
            create.Close();
            clone.Close();
        });
    }

    [Test]
    public void HostControls_ShouldLoad()
    {
        RunOnUiThread(() =>
        {
            new ImportControl().Should().NotBeNull();
            new SettingsControl().Should().NotBeNull();

            // The dialogs use the theme of the application and must not merge it from an old path
            var dialog = new CustomDialog();
            var confirm = new CustomerConfirmWindow(new Customer());
            dialog.Close();
            confirm.Close();
        });
    }
}
