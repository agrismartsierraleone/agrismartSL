using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Infrastructure.Harvest;

/// <summary>
/// Deterministic synthetic climate, for running the API with no outbound network: demos on a bad
/// connection, CI, and tests that must not depend on a third party being up. Enable with
/// "OpenMeteo:UseSyntheticProvider": true, or by setting the environment variable AGRISMART_OFFLINE=true.
///
/// The signal is a seasonal sinusoid whose amplitude grows with latitude, plus a per-year anomaly and
/// a per-day wobble. Both are derived from a hash of the date, so the same location always produces the
/// same series -- which is what makes the analog-year ensemble meaningful rather than noise.
///
/// This is NOT a climate model. Never point a real agronomic decision at it.
/// </summary>
public sealed class SyntheticWeatherProvider : IWeatherProvider
{
    public Task<IReadOnlyList<DailyTemperature>> GetArchiveAsync(
        double latitude, double longitude, DateOnly start, DateOnly end, CancellationToken cancellationToken = default) =>
        Task.FromResult(Generate(latitude, longitude, start, end, _ => TemperatureSource.Observed));

    public Task<IReadOnlyList<DailyTemperature>> GetRecentAndForecastAsync(
        double latitude, double longitude, int pastDays, int forecastDays, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-Math.Clamp(pastDays, 0, 92));
        var end = today.AddDays(Math.Clamp(forecastDays, 1, 16));

        return Task.FromResult(Generate(latitude, longitude, start, end,
            d => d <= today ? TemperatureSource.Observed : TemperatureSource.Forecast));
    }

    private static IReadOnlyList<DailyTemperature> Generate(
        double latitude, double longitude, DateOnly start, DateOnly end,
        Func<DateOnly, TemperatureSource> source)
    {
        if (end < start) return [];

        var days = new List<DailyTemperature>(end.DayNumber - start.DayNumber + 1);

        // Warm at the equator, colder and more seasonal toward the poles.
        var absLat = Math.Abs(latitude);
        var annualMean = 29.0 - absLat * 0.35;
        var seasonalAmplitude = absLat * 0.28;
        var southern = latitude < 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var dayOfYear = date.DayOfYear;
            var phase = 2 * Math.PI * (dayOfYear - (southern ? 200 : 20)) / 365.25;

            var yearAnomaly = (Hash(date.Year, (int)longitude, 7919) % 200 - 100) / 100.0 * 1.2;
            var dayWobble = (Hash(date.DayNumber, (int)(latitude * 10), 104729) % 200 - 100) / 100.0 * 2.5;

            var mean = annualMean + seasonalAmplitude * Math.Cos(phase) + yearAnomaly + dayWobble;
            var range = 9.0 + (Hash(date.DayNumber, 31, 15485863) % 100) / 100.0 * 4.0;

            days.Add(new DailyTemperature(
                date,
                Math.Round(mean + range / 2, 1),
                Math.Round(mean - range / 2, 1),
                source(date)));
        }

        return days;
    }

    /// <summary>Small stable integer hash. Deterministic across runs and platforms.</summary>
    private static int Hash(int a, int b, int salt)
    {
        unchecked
        {
            var h = 2166136261u;
            h = (h ^ (uint)a) * 16777619u;
            h = (h ^ (uint)b) * 16777619u;
            h = (h ^ (uint)salt) * 16777619u;
            return (int)(h % int.MaxValue);
        }
    }
}
