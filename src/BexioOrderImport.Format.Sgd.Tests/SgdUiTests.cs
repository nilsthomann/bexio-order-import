using System.Globalization;
using System.Reflection;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.Sgd.Resources;
using BexioOrderImport.Format.Sgd.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Format.Sgd.Tests;

/// <summary>
/// Controls with the host theme and the translations of the SGD format.
/// </summary>
[NotInParallel]
public class SgdUiTests
{
    private static Dictionary<string, string?> ReadTranslations(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return typeof(SgdTranslations)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string))
                .ToDictionary(p => p.Name, p => p.GetValue(null) as string);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Test]
    [Arguments("de-CH")]
    [Arguments("en-US")]
    public void Translations_ShouldReturnNonEmptyStrings(string cultureName)
    {
        var values = ReadTranslations(cultureName);

        values.Should().NotBeEmpty();
        values.Where(v => string.IsNullOrWhiteSpace(v.Value)).Select(v => v.Key).Should().BeEmpty();
    }

    [Test]
    public void Translations_ShouldUseSwissSpellingAndDifferPerLanguage()
    {
        var german = ReadTranslations("de-CH");

        german.Where(v => v.Value!.Contains('ß')).Select(v => v.Key).Should().BeEmpty();
        ReadTranslations("en-US")[nameof(SgdTranslations.Format_Description)].Should().NotBe(german[nameof(SgdTranslations.Format_Description)]);
    }

    [Test]
    public void ProfileConfigControl_ShouldLoadValidateAndSaveSettings()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new SgdUiProvider().CreateProfileConfigControl();

            control.Load(new SgdSettings { SizeColumn = "G" });

            control.View.DataContext.Should().BeOfType<SgdProfileConfigViewModel>();
            control.Validate().Should().BeEmpty();
            control.Save().Should().BeOfType<SgdSettings>().Which.SizeColumn.Should().Be("G");
        });
    }

    [Test]
    public void PreviewControl_ShouldShowAndClearAnOrder()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new SgdUiProvider().CreateImportPreviewControl();

            control.HeaderView.Should().BeOfType<SgdOrderHeaderControl>();
            control.PositionsView.Should().BeOfType<SgdPositionsControl>();

            control.Show(new Order { Positions = [new OrderPosition { ArticleNumber = "E1", Size = "1x S<br />2x M", Quantity = 3 }] }, new SgdSettings());
            control.HeaderView.DataContext.Should().BeOfType<SgdPreviewViewModel>();
            control.PositionsView.DataContext.Should().BeSameAs(control.HeaderView.DataContext);

            control.Clear();
            control.HeaderView.DataContext.Should().BeNull();
            control.PositionsView.DataContext.Should().BeNull();
        });
    }

    [Test]
    public void UiProvider_ShouldBelongToTheSgdFormat()
    {
        new SgdUiProvider().FormatId.Should().Be(SgdFormatAdapter.Id);
    }

    [Test]
    public void ProfileConfigControl_InputsShouldKeepTheThemeStyle()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new SgdUiProvider().CreateProfileConfigControl();

            // A local TextBox style without BasedOn would replace the theme style (rounded border)
            var localStyle = (System.Windows.Style)control.View.FindResource(typeof(System.Windows.Controls.TextBox));
            var themeStyle = System.Windows.Application.Current.FindResource(typeof(System.Windows.Controls.TextBox));

            localStyle.Should().NotBeSameAs(themeStyle);
            localStyle.BasedOn.Should().BeSameAs(themeStyle);
        });
    }
}
