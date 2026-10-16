using System.Globalization;
using System.Reflection;
using BexioOrderImport.Wpf.Resources;
using FluentAssertions;

namespace BexioOrderImport.Tests;

/// <summary>
/// Host translations. Each format tests its own translations in its test project.
/// </summary>
public class TranslationIntegrityTests
{
    private static Dictionary<string, string?> ReadAll(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return typeof(Translations)
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
    public void AllTranslationProperties_ShouldReturnNonEmptyString_ForSupportedCultures(string cultureName)
    {
        var values = ReadAll(cultureName);

        values.Should().NotBeEmpty("Translations class should define static string properties");
        values.Where(v => string.IsNullOrWhiteSpace(v.Value)).Select(v => v.Key).Should().BeEmpty(
            $"All Translation properties in Translations.cs must resolve to non-empty strings in resx for culture '{cultureName}'");
    }

    [Test]
    public void GermanTranslations_ShouldUseSwissSpelling()
    {
        ReadAll("de-CH").Where(v => v.Value!.Contains('ß')).Select(v => v.Key).Should().BeEmpty(
            "Swiss German writes \"ss\" instead of \"ß\"");
    }
}
