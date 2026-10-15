using System.Windows;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Peakmile;
using BexioOrderImport.Tests.Utils;
using BexioOrderImport.Wpf.Views;
using BexioOrderImport.Wpf.Views.Controls;
using FluentAssertions;
using WpfApp = System.Windows.Application;

namespace BexioOrderImport.Tests;

/// <summary>
/// Loads the XAML of the format controls and host views with the shared theme, so missing resources fail here and not at runtime.
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
    public void PeakmileProfileConfigControl_ShouldLoadValidateAndSaveSettings()
    {
        RunOnUiThread(() =>
        {
            var control = new PeakmileUiProvider().CreateProfileConfigControl();

            control.Load(new PeakmileSettings { SeasonCode = "HW26" });

            control.View.Should().NotBeNull();
            control.Validate().Should().BeEmpty();
            control.Save().Should().BeOfType<PeakmileSettings>().Which.SeasonCode.Should().Be("HW26");
        });
    }

    [Test]
    public void PeakmilePreviewControl_ShouldShowAndClearAnOrder()
    {
        RunOnUiThread(() =>
        {
            var control = new PeakmileUiProvider().CreateImportPreviewControl();
            var order = new Order { Positions = [new OrderPosition { ArticleNumber = "A1", Quantity = 2, GrossUnitPrice = 5m }] };

            control.Show(order, new PeakmileSettings());
            control.View.DataContext.Should().BeOfType<PeakmilePreviewViewModel>();

            control.Clear();
            control.View.DataContext.Should().BeNull();
        });
    }

    [Test]
    public void ProfileEditWindow_ShouldHostTheFormatControl()
    {
        RunOnUiThread(() =>
        {
            var profile = ProfileTestExtensions.PeakmileProfile("Default");

            var window = new ProfileEditWindow(profile, new PeakmileFormatAdapter(), new PeakmileUiProvider());

            window.FormatSettingsHost.Content.Should().BeOfType<PeakmileProfileConfigControl>();
            window.FormatInput.IsEnabled.Should().BeFalse();
            window.Close();
        });
    }

    [Test]
    public void ProfileCreateDialog_WhenCloning_ShouldFixTheFormat()
    {
        RunOnUiThread(() =>
        {
            var formats = new[] { new PeakmileFormatAdapter() };

            var create = new ProfileCreateDialog(formats);
            var clone = new ProfileCreateDialog(formats, PeakmileFormatAdapter.Id);

            create.FormatInput.IsEnabled.Should().BeTrue();
            create.FormatInput.SelectedValue.Should().Be(PeakmileFormatAdapter.Id);
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
        });
    }
}
