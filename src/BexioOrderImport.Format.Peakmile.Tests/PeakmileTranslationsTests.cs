using System.Globalization;
using System.Reflection;
using BexioOrderImport.Format.Peakmile.Resources;
using FluentAssertions;

namespace BexioOrderImport.Format.Peakmile.Tests;

public class PeakmileTranslationsTests
{
    private static Dictionary<string, string?> ReadAll(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return typeof(PeakmileTranslations)
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
    public void AllProperties_ShouldReturnNonEmptyString_ForSupportedCultures(string cultureName)
    {
        var values = ReadAll(cultureName);

        values.Should().NotBeEmpty();
        values.Where(v => string.IsNullOrWhiteSpace(v.Value)).Select(v => v.Key).Should().BeEmpty(
            $"every PeakmileTranslations property needs a resx entry for '{cultureName}'");
    }

    [Test]
    public void EnglishTranslations_ShouldDifferFromGerman()
    {
        ReadAll("en-US")[nameof(PeakmileTranslations.Format_DisplayName)].Should().NotBe(ReadAll("de-CH")[nameof(PeakmileTranslations.Format_DisplayName)]);
    }

    [Test]
    public void GermanTranslations_ShouldUseSwissSpelling()
    {
        ReadAll("de-CH").Where(v => v.Value!.Contains('ß')).Select(v => v.Key).Should().BeEmpty(
            "Swiss German writes \"ss\" instead of \"ß\"");
    }
}
