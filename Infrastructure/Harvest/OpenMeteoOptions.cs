namespace AgrismartAPI.Infrastructure.Harvest;

public class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    public string ArchiveBaseUrl { get; set; } = "https://archive-api.open-meteo.com";
    public string ForecastBaseUrl { get; set; } = "https://api.open-meteo.com";

    /// <summary>How many past years of daily temperatures to pull for the analog-year ensemble.</summary>
    public int AnalogYears { get; set; } = 10;

    /// <summary>Days of NWP forecast to trust before falling back to climatology. Open-Meteo caps at 16.</summary>
    public int ForecastDays { get; set; } = 16;

    /// <summary>Cache lifetime for the multi-year archive pull, in hours.</summary>
    public int ArchiveCacheHours { get; set; } = 12;

    /// <summary>
    /// Serve deterministic synthetic weather instead of calling Open-Meteo. For offline demos and
    /// tests only. Also switched on by AGRISMART_OFFLINE=true.
    /// </summary>
    public bool UseSyntheticProvider { get; set; }
}
