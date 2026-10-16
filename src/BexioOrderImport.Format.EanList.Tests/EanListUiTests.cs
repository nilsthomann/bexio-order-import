using System.Globalization;
using System.Reflection;
using BexioOrderImport.Domain.Models;
using BexioOrderImport.Format.EanList.Resources;
using BexioOrderImport.Format.EanList.Tests.Utils;
using FluentAssertions;

namespace BexioOrderImport.Format.EanList.Tests;

/// <summary>
/// Controls with the host theme and the translations of the EAN list format.
/// </summary>
[NotInParallel]
public class EanListUiTests
{
    private static Dictionary<string, string?> ReadTranslations(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return typeof(EanListTranslations)
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
        ReadTranslations("en-US")[nameof(EanListTranslations.Format_DisplayName)].Should().NotBe(german[nameof(EanListTranslations.Format_DisplayName)]);
    }

    [Test]
    public void ProfileConfigControl_ShouldLoadValidateAndSaveSettings()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new EanListUiProvider().CreateProfileConfigControl();

            control.Load(new EanListSettings { EanColumn = "E" });

            control.View.DataContext.Should().BeOfType<EanListProfileConfigViewModel>();
            control.Validate().Should().BeEmpty();
            control.Save().Should().BeOfType<EanListSettings>().Which.EanColumn.Should().Be("E");
        });
    }

    [Test]
    public void PreviewControl_ShouldShowAndClearAnOrder()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new EanListUiProvider().CreateImportPreviewControl();

            control.HeaderView.Should().BeOfType<EanListOrderHeaderControl>();
            control.PositionsView.Should().BeOfType<EanListPositionsControl>();

            control.Show(new Order { Positions = [new OrderPosition { Ean = "1", Quantity = 1 }] }, new EanListSettings());
            control.HeaderView.DataContext.Should().BeOfType<EanListPreviewViewModel>();
            control.PositionsView.DataContext.Should().BeSameAs(control.HeaderView.DataContext);

            control.Clear();
            control.HeaderView.DataContext.Should().BeNull();
            control.PositionsView.DataContext.Should().BeNull();
        });
    }

    [Test]
    public void UiProvider_ShouldBelongToTheEanListFormat()
    {
        new EanListUiProvider().FormatId.Should().Be(EanListFormatAdapter.Id);
    }

    [Test]
    public void ProfileConfigControl_InputsShouldKeepTheThemeStyle()
    {
        WpfTestApplication.RunOnUiThread(() =>
        {
            var control = new EanListUiProvider().CreateProfileConfigControl();

            // A local TextBox style without BasedOn would replace the theme style (rounded border)
            var localStyle = (System.Windows.Style)control.View.FindResource(typeof(System.Windows.Controls.TextBox));
            var themeStyle = System.Windows.Application.Current.FindResource(typeof(System.Windows.Controls.TextBox));

            localStyle.Should().NotBeSameAs(themeStyle);
            localStyle.BasedOn.Should().BeSameAs(themeStyle);
        });
    }
}
