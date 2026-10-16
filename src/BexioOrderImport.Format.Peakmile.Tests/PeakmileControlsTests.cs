using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Peakmile.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Format.Peakmile.Tests;

/// <summary>
/// Loads the XAML of the Peakmile controls with the host theme, so missing resources fail here and not at runtime.
/// </summary>
[NotInParallel]
public class PeakmileControlsTests
{
    [Test]
    public void ProfileConfigControl_ShouldLoadValidateAndSaveSettings()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new PeakmileUiProvider().CreateProfileConfigControl();

            control.Load(new PeakmileSettings { SeasonCode = "HW26" });

            control.View.Should().BeOfType<PeakmileProfileConfigControl>();
            control.View.DataContext.Should().BeOfType<PeakmileProfileConfigViewModel>();
            control.Validate().Should().BeEmpty();
            control.Save().Should().BeOfType<PeakmileSettings>().Which.SeasonCode.Should().Be("HW26");
        });
    }

    [Test]
    public void PreviewControl_ShouldShowAndClearAnOrder()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new PeakmileUiProvider().CreateImportPreviewControl();
            var order = new Order { Positions = [new OrderPosition { ArticleNumber = "A1", Quantity = 2, GrossUnitPrice = 5m }] };

            control.HeaderView.Should().BeOfType<PeakmileOrderHeaderControl>();
            control.PositionsView.Should().BeOfType<PeakmilePositionsControl>();

            control.Show(order, new PeakmileSettings());
            control.HeaderView.DataContext.Should().BeOfType<PeakmilePreviewViewModel>();
            control.PositionsView.DataContext.Should().BeSameAs(control.HeaderView.DataContext);

            control.Clear();
            control.HeaderView.DataContext.Should().BeNull();
            control.PositionsView.DataContext.Should().BeNull();
        });
    }

    [Test]
    public void UiProvider_ShouldBelongToThePeakmileFormat()
    {
        new PeakmileUiProvider().FormatId.Should().Be(PeakmileFormatAdapter.Id);
    }

    [Test]
    public void ProfileConfigControl_InputsShouldKeepTheThemeStyle()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new PeakmileUiProvider().CreateProfileConfigControl();

            // A local TextBox style without BasedOn would replace the theme style (rounded border)
            var localStyle = (System.Windows.Style)control.View.FindResource(typeof(System.Windows.Controls.TextBox));
            var themeStyle = System.Windows.Application.Current.FindResource(typeof(System.Windows.Controls.TextBox));

            localStyle.Should().NotBeSameAs(themeStyle);
            localStyle.BasedOn.Should().BeSameAs(themeStyle);
        });
    }
}
