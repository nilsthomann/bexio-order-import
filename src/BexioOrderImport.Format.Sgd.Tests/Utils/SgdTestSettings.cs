namespace BexioOrderImport.Format.Sgd.Tests.Utils;

/// <summary>
/// The season names of the fixture and TempWorkbook and their Bexio season codes.
/// </summary>
public static class SgdTestSettings
{
    public const string FallWinter = "2026 Fall/Winter";
    public const string SpringSummer = "2027 Spring/Summer";

    /// <summary>
    /// The settings with the mappings FallWinter → HW26 and SpringSummer → FS27.
    /// </summary>
    public static SgdSettings WithSeasons(SgdSettings? settings = null)
    {
        settings ??= new SgdSettings();
        settings.SeasonMappings =
        [
            new SgdSeasonMapping { ExternalSeason = FallWinter, SeasonCode = "HW26" },
            new SgdSeasonMapping { ExternalSeason = SpringSummer, SeasonCode = "FS27" }
        ];
        return settings;
    }
}
