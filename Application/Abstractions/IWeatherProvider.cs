using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Application.Abstractions;

/// <summary>Pluggable daily-temperature source for harvest prediction. Swap the registered
/// implementation to move from synthetic (offline) data to a real weather API.</summary>
public interface IWeatherProvider
{
    /// <summary>Daily observed temperatures from the reanalysis archive.</summary>
    Task<IReadOnlyList<DailyTemperature>> GetArchiveAsync(
        double latitude, double longitude, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>Recent past (filling the archive's few-day lag) plus the NWP forecast.</summary>
    Task<IReadOnlyList<DailyTemperature>> GetRecentAndForecastAsync(
        double latitude, double longitude, int pastDays, int forecastDays, CancellationToken cancellationToken = default);
}
