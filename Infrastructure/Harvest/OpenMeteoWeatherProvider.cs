using System.Globalization;
using System.Text.Json.Serialization;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Harvest;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure.Harvest;

public sealed class OpenMeteoWeatherProvider(
    HttpClient http,
    IMemoryCache cache,
    IOptions<OpenMeteoOptions> options,
    ILogger<OpenMeteoWeatherProvider> logger) : IWeatherProvider
{
    private readonly OpenMeteoOptions _options = options.Value;

    public async Task<IReadOnlyList<DailyTemperature>> GetArchiveAsync(
        double latitude, double longitude, DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        if (end < start) return [];

        // Cache on a coarse grid: a 0.1 deg cell is ~11 km, well inside the model's own
        // resolution, so neighbouring farms share one upstream call.
        var key = $"harvest-archive:{Round(latitude)}:{Round(longitude)}:{start:yyyyMMdd}:{end:yyyyMMdd}";

        if (cache.TryGetValue(key, out IReadOnlyList<DailyTemperature>? cached) && cached is not null)
            return cached;

        var url = $"{_options.ArchiveBaseUrl}/v1/archive" +
                  $"?latitude={Fmt(latitude)}&longitude={Fmt(longitude)}" +
                  $"&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}" +
                  "&daily=temperature_2m_max,temperature_2m_min" +
                  "&timezone=UTC";

        var result = await FetchAsync(url, TemperatureSource.Observed, cancellationToken);

        cache.Set(key, result, TimeSpan.FromHours(_options.ArchiveCacheHours));
        return result;
    }

    public async Task<IReadOnlyList<DailyTemperature>> GetRecentAndForecastAsync(
        double latitude, double longitude, int pastDays, int forecastDays, CancellationToken cancellationToken = default)
    {
        pastDays = Math.Clamp(pastDays, 0, 92);      // Open-Meteo limit
        forecastDays = Math.Clamp(forecastDays, 1, 16);

        var key = $"harvest-forecast:{Round(latitude)}:{Round(longitude)}:{pastDays}:{forecastDays}:{DateTime.UtcNow:yyyyMMddHH}";

        if (cache.TryGetValue(key, out IReadOnlyList<DailyTemperature>? cached) && cached is not null)
            return cached;

        var url = $"{_options.ForecastBaseUrl}/v1/forecast" +
                  $"?latitude={Fmt(latitude)}&longitude={Fmt(longitude)}" +
                  $"&past_days={pastDays}&forecast_days={forecastDays}" +
                  "&daily=temperature_2m_max,temperature_2m_min" +
                  "&timezone=UTC";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Everything up to and including today is effectively observed; after that it is a forecast.
        var result = await FetchAsync(url, TemperatureSource.Forecast, cancellationToken, d =>
            d <= today ? TemperatureSource.Observed : TemperatureSource.Forecast);

        cache.Set(key, result, TimeSpan.FromHours(1));
        return result;
    }

    private async Task<IReadOnlyList<DailyTemperature>> FetchAsync(
        string url,
        TemperatureSource defaultSource,
        CancellationToken cancellationToken,
        Func<DateOnly, TemperatureSource>? sourceSelector = null)
    {
        logger.LogDebug("Open-Meteo GET {Url}", url);

        using var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ExternalServiceException(
                $"Open-Meteo returned {(int)response.StatusCode}: {Truncate(body, 300)}");
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(cancellationToken: cancellationToken)
                      ?? throw new ExternalServiceException("Open-Meteo returned an empty body.");

        var daily = payload.Daily;
        if (daily?.Time is null || daily.MaxC is null || daily.MinC is null)
            throw new ExternalServiceException("Open-Meteo response did not contain daily temperatures.");

        var days = new List<DailyTemperature>(daily.Time.Length);

        for (var i = 0; i < daily.Time.Length; i++)
        {
            // Gaps happen at the edges of the archive; skip rather than guess.
            if (daily.MaxC[i] is not { } max || daily.MinC[i] is not { } min) continue;
            if (!DateOnly.TryParse(daily.Time[i], CultureInfo.InvariantCulture, out var date)) continue;

            days.Add(new DailyTemperature(date, max, min, sourceSelector?.Invoke(date) ?? defaultSource));
        }

        return days;
    }

    private static string Fmt(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

    private static string Round(double value) =>
        Math.Round(value, 1).ToString("F1", CultureInfo.InvariantCulture);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    // --- wire format -----------------------------------------------------

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("daily")] public DailyBlock? Daily { get; set; }
    }

    private sealed class DailyBlock
    {
        [JsonPropertyName("time")] public string[]? Time { get; set; }
        [JsonPropertyName("temperature_2m_max")] public double?[]? MaxC { get; set; }
        [JsonPropertyName("temperature_2m_min")] public double?[]? MinC { get; set; }
    }
}
