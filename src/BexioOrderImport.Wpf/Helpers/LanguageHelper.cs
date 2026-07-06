using System.Globalization;
using System.Threading;

namespace BexioOrderImport.Wpf.Helpers;

/// <summary>
/// Centralizes UI culture/language switching so both App.xaml.cs and MainViewModel
/// use the same logic instead of duplicating it.
/// </summary>
public static class LanguageHelper
{
    /// <summary>
    /// Applies the given language code ("de" or "en") to the current thread and all
    /// new threads by setting <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/>.
    /// </summary>
    public static void Apply(string languageCode)
    {
        var culture = new CultureInfo(languageCode == "en" ? "en-US" : "de-CH");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
