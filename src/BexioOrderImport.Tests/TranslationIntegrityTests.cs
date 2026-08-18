using System.Globalization;
using System.Reflection;
using BexioOrderImport.Wpf.Resources;
using FluentAssertions;

namespace BexioOrderImport.Tests;

public class TranslationIntegrityTests
{
    [Test]
    [Arguments("de-CH")]
    [Arguments("en-US")]
    public void AllTranslationProperties_ShouldReturnNonEmptyString_ForSupportedCultures(string cultureName)
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentUICulture;
        var testCulture = new CultureInfo(cultureName);

        try
        {
            CultureInfo.CurrentUICulture = testCulture;

            var properties = typeof(Translations)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string))
                .ToList();

            properties.Should().NotBeEmpty("Translations class should define static string properties");

            // Act & Assert
            var missingKeys = new List<string>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(value))
                {
                    missingKeys.Add(prop.Name);
                }
            }

            missingKeys.Should().BeEmpty(
                $"All Translation properties in Translations.cs must resolve to non-empty strings in resx for culture '{cultureName}'");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
