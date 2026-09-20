using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Harvest;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure.Harvest;

/// <summary>
/// Growing-degree-day harvest predictor with an analog-year ensemble for the part of the season
/// that has not happened yet.
///
/// The season is split into three segments:
///   1. planting -> yesterday        reanalysis (what actually happened)
///   2. today    -> today + 16 days  numerical weather forecast
///   3. beyond                       replay of the last N years' real temperatures
///
/// Segment 3 is the interesting one. Rather than inventing a symmetric error bar, we re-run the
/// remaining thermal requirement against each of the last N years' actual weather. That yields N
/// plausible harvest dates; the 10th/50th/90th percentiles become the earliest / expected / latest
/// window. The spread is then a real statement about how variable this location's climate is, not
/// a guess.
/// </summary>
public sealed class GddHarvestPredictor(
    IWeatherProvider weather,
    IOptions<OpenMeteoOptions> options,
    ILogger<GddHarvestPredictor> logger) : IHarvestPredictor
{
    private readonly OpenMeteoOptions _options = options.Value;

    /// <summary>The reanalysis archive lags real time by a few days; stay behind it.</summary>
    private const int ArchiveLagDays = 6;

    /// <summary>Give up rather than spin forever on a crop that will never mature here.</summary>
    private const int MaxProjectionDays = 730;

    public async Task<HarvestPredictionResult> PredictAsync(
        CropProfile crop,
        double latitude,
        double longitude,
        DateOnly plantingDate,
        double? gddToMaturityOverride,
        CancellationToken cancellationToken = default)
    {
        var required = gddToMaturityOverride is > 0 ? gddToMaturityOverride.Value : crop.GddToMaturity;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var planting = plantingDate;

        // ---- 1. Pull weather -------------------------------------------------
        // One archive call covers both the elapsed part of this season and the analog years;
        // one forecast call covers the near term. Both are cached.

        var analogFirstYear = today.Year - _options.AnalogYears;
        var archiveStart = Min(planting, new DateOnly(analogFirstYear, 1, 1));
        var archiveEnd = today.AddDays(-ArchiveLagDays);

        var archiveTask = weather.GetArchiveAsync(latitude, longitude, archiveStart, archiveEnd, cancellationToken);

        var pastDays = planting < today
            ? Math.Clamp(today.DayNumber - planting.DayNumber, 0, 92)
            : 0;

        var forecastTask = weather.GetRecentAndForecastAsync(latitude, longitude, pastDays, _options.ForecastDays, cancellationToken);

        await Task.WhenAll(archiveTask, forecastTask);

        // Forecast wins on overlap: it is fresher than the reanalysis near today.
        var byDate = new Dictionary<DateOnly, DailyTemperature>();
        foreach (var day in await archiveTask) byDate[day.Date] = day;
        foreach (var day in await forecastTask) byDate[day.Date] = day;

        if (byDate.Count == 0)
            throw new ExternalServiceException("No weather data was returned for that location.");

        // ---- 2. Walk the known part of the season ----------------------------

        var known = byDate.Values
            .Where(d => d.Date >= planting)
            .OrderBy(d => d.Date)
            .ToList();

        var trace = new List<DailyGddPoint>();
        double cumulative = 0;
        double cumulativeThroughToday = 0;
        int daysObserved = 0, daysForecast = 0;
        DateOnly? maturedOn = null;

        foreach (var day in known)
        {
            var gdd = DailyGdd(day.MaxC, day.MinC, crop.BaseTemperatureC, crop.UpperTemperatureC);
            cumulative += gdd;

            if (day.Source == TemperatureSource.Observed) daysObserved++; else daysForecast++;
            if (day.Date <= today) cumulativeThroughToday = cumulative;

            trace.Add(new DailyGddPoint(day.Date, day.MaxC, day.MinC, Round1(gdd), Round1(cumulative), day.Source));

            if (maturedOn is null && cumulative >= required) maturedOn = day.Date;
        }

        var percent = required > 0 ? Math.Clamp(cumulativeThroughToday / required * 100.0, 0, 100) : 0;

        // ---- 3a. Already there, or will be inside the forecast window --------

        if (maturedOn is { } certainDate)
        {
            var fromObserved = certainDate <= today;

            var window = new HarvestWindow(
                Earliest: certainDate,
                Expected: certainDate,
                Latest: certainDate,
                DaysFromNow: certainDate.DayNumber - today.DayNumber,
                WindowWidthDays: 0,
                Confidence: "High",
                Basis: fromObserved
                    ? "Thermal requirement already met by observed weather."
                    : "Thermal requirement is met inside the 16-day forecast window.");

            return Build(crop, required, today, fromObserved,
                cumulativeThroughToday, percent, daysObserved, daysForecast,
                window, trace, analogsUsed: 0,
                notes: "No climatological projection was needed.");
        }

        // ---- 3b. Project past the forecast horizon with analog years ---------

        var remaining = required - cumulative;
        var lastKnown = known.Count > 0 ? known[^1].Date : planting.AddDays(-1);
        var projectionStart = Max(lastKnown.AddDays(1), planting);

        var analogs = RunAnalogEnsemble(
            byDate, crop, remaining, projectionStart, analogFirstYear, today.Year - 1);

        if (analogs.Count == 0)
            throw new ExternalServiceException(
                "Not enough historical weather at this location to project a harvest date.");

        var durations = analogs.Select(a => a.Days).OrderBy(d => d).ToList();

        var earliestDays = Percentile(durations, 0.10);
        var expectedDays = Percentile(durations, 0.50);
        var latestDays = Percentile(durations, 0.90);

        var earliest = projectionStart.AddDays(earliestDays - 1);
        var expected = projectionStart.AddDays(expectedDays - 1);
        var latest = projectionStart.AddDays(latestDays - 1);

        var width = latest.DayNumber - earliest.DayNumber;

        var harvest = new HarvestWindow(
            Earliest: earliest,
            Expected: expected,
            Latest: latest,
            DaysFromNow: expected.DayNumber - today.DayNumber,
            WindowWidthDays: width,
            Confidence: width <= 7 ? "High" : width <= 16 ? "Medium" : "Low",
            Basis: $"P10/P50/P90 of {analogs.Count} analog years replayed from {projectionStart:yyyy-MM-dd}.");

        // Extend the trace along the median analog path so the curve the caller plots is the same
        // one that produced the expected date. Built even when the trace is not returned, because
        // stage dating reads off it.
        var medianAnalog = analogs.OrderBy(a => a.Days).ElementAt(analogs.Count / 2);
        foreach (var step in medianAnalog.Path)
        {
            cumulative += step.Gdd;
            trace.Add(new DailyGddPoint(
                projectionStart.AddDays(step.Offset),
                step.MaxC, step.MinC,
                Round1(step.Gdd), Round1(cumulative),
                TemperatureSource.Climatology));
        }

        var extrapolated = analogs.Count(a => a.Extrapolated);

        return Build(crop, required, today, alreadyMature: false,
            cumulativeThroughToday, percent, daysObserved, daysForecast,
            harvest, trace, analogs.Count,
            notes: extrapolated == 0
                ? "All analog years had complete weather coverage."
                : $"{extrapolated} of {analogs.Count} analog years ran past the archive and were extended at their mean daily GDD.");
    }

    // ---------------------------------------------------------------------
    // Analog-year ensemble
    // ---------------------------------------------------------------------

    private sealed record AnalogResult(int Year, int Days, bool Extrapolated, List<PathStep> Path);

    private sealed record PathStep(int Offset, double MaxC, double MinC, double Gdd);

    /// <summary>
    /// For each past year, start on the same calendar day as <paramref name="projectionStart"/> and
    /// accumulate that year's real temperatures until the remaining GDD is met.
    /// </summary>
    private List<AnalogResult> RunAnalogEnsemble(
        IReadOnlyDictionary<DateOnly, DailyTemperature> history,
        CropProfile crop,
        double remaining,
        DateOnly projectionStart,
        int firstYear,
        int lastYear)
    {
        var results = new List<AnalogResult>();

        for (var year = firstYear; year <= lastYear; year++)
        {
            var start = SafeDate(year, projectionStart.Month, projectionStart.Day);

            double accumulated = 0, gddSum = 0;
            var path = new List<PathStep>();
            var covered = 0;
            var matured = false;

            for (var offset = 0; offset < MaxProjectionDays; offset++)
            {
                if (!history.TryGetValue(start.AddDays(offset), out var day))
                    break; // ran off the end of the archive

                var gdd = DailyGdd(day.MaxC, day.MinC, crop.BaseTemperatureC, crop.UpperTemperatureC);
                accumulated += gdd;
                gddSum += gdd;
                covered++;
                path.Add(new PathStep(offset, day.MaxC, day.MinC, gdd));

                if (accumulated >= remaining)
                {
                    results.Add(new AnalogResult(year, offset + 1, false, path));
                    matured = true;
                    break;
                }
            }

            if (matured) continue;

            // The analog ran out of data. Rather than discard the year, extend it at the mean
            // daily rate it did establish. Flagged in the response.
            if (covered < 14 || gddSum <= 0)
            {
                logger.LogDebug("Analog year {Year} discarded: only {Days} days of data.", year, covered);
                continue;
            }

            var meanDaily = gddSum / covered;
            var extraDays = (int)Math.Ceiling((remaining - accumulated) / meanDaily);
            var total = covered + Math.Max(extraDays, 0);

            if (total > MaxProjectionDays) continue;

            results.Add(new AnalogResult(year, total, true, path));
        }

        return results;
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    /// <summary>
    /// Modified ("Method B") growing degree days: both bounds are clamped into [Tbase, Tupper]
    /// before averaging. This is what most extension services publish, and it avoids the classic
    /// bug where a hot afternoon inflates development on a crop that has already stopped responding
    /// to heat.
    /// </summary>
    internal static double DailyGdd(double maxC, double minC, double baseC, double upperC)
    {
        if (maxC < minC) (maxC, minC) = (minC, maxC);

        var hi = Math.Clamp(maxC, baseC, upperC);
        var lo = Math.Clamp(minC, baseC, upperC);

        return Math.Max(0, (hi + lo) / 2.0 - baseC);
    }

    private static int Percentile(IReadOnlyList<int> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        var index = (int)Math.Round(p * (sorted.Count - 1), MidpointRounding.AwayFromZero);
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static DateOnly SafeDate(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static double Round1(double v) => Math.Round(v, 1);

    private static HarvestPredictionResult Build(
        CropProfile crop, double required,
        DateOnly today, bool alreadyMature,
        double accumulated, double percent, int daysObserved, int daysForecast,
        HarvestWindow harvest, List<DailyGddPoint> trace, int analogsUsed, string notes)
    {
        var stage = ResolveStage(crop, required, accumulated, trace, today);

        var gdd = new GddSummary(
            Accumulated: Round1(accumulated),
            Required: Round1(required),
            Remaining: Round1(Math.Max(0, required - accumulated)),
            PercentComplete: Math.Round(percent, 1),
            BaseTemperatureC: crop.BaseTemperatureC,
            UpperTemperatureC: crop.UpperTemperatureC,
            DaysObserved: daysObserved,
            DaysForecast: daysForecast);

        var methodology = new MethodologyInfo(
            Model: "Modified growing-degree-day (Method B) with analog-year ensemble",
            WeatherSource: "Open-Meteo archive + forecast (no API key)",
            AnalogYears: analogsUsed,
            Notes: notes);

        return new HarvestPredictionResult(
            AsOf: today,
            AlreadyMature: alreadyMature,
            Gdd: gdd,
            Stage: stage,
            Harvest: harvest,
            Methodology: methodology,
            DailyTrace: trace);
    }

    private static GrowthStageInfo ResolveStage(
        CropProfile crop, double required, double accumulated,
        IReadOnlyList<DailyGddPoint> trace, DateOnly today)
    {
        var fraction = required > 0 ? accumulated / required : 0;

        var ordered = crop.Stages.OrderBy(s => s.StartFraction).ToList();
        var currentIndex = -1;

        for (var i = 0; i < ordered.Count; i++)
            if (fraction >= ordered[i].StartFraction) currentIndex = i;

        var current = currentIndex >= 0 ? ordered[currentIndex].Name : "Pre-planting";
        var next = currentIndex + 1 < ordered.Count ? ordered[currentIndex + 1] : null;

        DateOnly? nextStarts = null;
        if (next is not null)
        {
            var threshold = next.StartFraction * required;
            // First day on the projected curve that crosses the next stage's threshold.
            nextStarts = trace.FirstOrDefault(p => p.Date > today && p.Cumulative >= threshold)?.Date;
        }

        return new GrowthStageInfo(current, next?.Name, nextStarts);
    }
}
