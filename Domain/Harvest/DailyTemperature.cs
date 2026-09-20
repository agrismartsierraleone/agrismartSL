namespace AgrismartAPI.Domain.Harvest;

/// <summary>Where a day's temperatures came from. Surfaced in the response so the caller can see
/// how much of the prediction rests on measurement vs. projection.</summary>
public enum TemperatureSource
{
    /// <summary>Reanalysis / observed record (Open-Meteo archive).</summary>
    Observed,

    /// <summary>Numerical weather forecast, typically the next 16 days.</summary>
    Forecast,

    /// <summary>Replayed from a historical analog year (climatology).</summary>
    Climatology
}

public record DailyTemperature(DateOnly Date, double MaxC, double MinC, TemperatureSource Source);
