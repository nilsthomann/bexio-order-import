using System.Resources;

namespace BexioOrderImport.Format.Peakmile.Resources;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class PeakmileTranslations
{
    private static readonly ResourceManager ResourceManager =
        new ResourceManager("BexioOrderImport.Format.Peakmile.Resources.PeakmileTranslations", typeof(PeakmileTranslations).Assembly);

    public static string Format_DisplayName => ResourceManager.GetString(nameof(Format_DisplayName)) ?? string.Empty;
    public static string Format_Description => ResourceManager.GetString(nameof(Format_Description)) ?? string.Empty;
}
