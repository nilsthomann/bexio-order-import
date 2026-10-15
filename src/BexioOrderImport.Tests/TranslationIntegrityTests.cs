using System.Globalization;
using System.Reflection;
using BexioOrderImport.Format.Peakmile.Resources;
using BexioOrderImport.Wpf.Resources;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class TranslationIntegrityTests
{
    private static readonly Type[] TranslationClasses = [typeof(Translations), typeof(PeakmileTranslations)];

    private static Dictionary<string, string?> ReadAll(Type translations, string cultureName)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(cultureName);
            return translations
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string))
                .ToDictionary(p => $"{translations.Name}.{p.Name}", p => p.GetValue(null) as string);
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
        foreach (var translations in TranslationClasses)
        {
            var values = ReadAll(translations, cultureName);

            values.Should().NotBeEmpty($"{translations.Name} should define static string properties");
            values.Where(v => string.IsNullOrWhiteSpace(v.Value)).Select(v => v.Key).Should().BeEmpty(
                $"all properties of {translations.Name} must resolve to non-empty strings for culture '{cultureName}'");
        }
    }

    [Test]
    public void GermanTranslations_ShouldUseSwissSpelling()
    {
        foreach (var translations in TranslationClasses)
        {
            ReadAll(translations, "de-CH").Where(v => v.Value!.Contains('ß')).Select(v => v.Key).Should().BeEmpty(
                "Swiss German writes \"ss\" instead of \"ß\"");
        }
    }
}
