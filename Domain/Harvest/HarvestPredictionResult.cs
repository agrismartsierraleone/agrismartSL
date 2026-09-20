namespace AgrismartAPI.Domain.Harvest;

public record GddSummary(
    double Accumulated,
    double Required,
    double Remaining,
    double PercentComplete,
    double BaseTemperatureC,
    double UpperTemperatureC,
    int DaysObserved,
    int DaysForecast);

public record GrowthStageInfo(
    string Current,
    string? Next,
    DateOnly? NextStageStarts);

public record HarvestWindow(
    DateOnly Earliest,
    DateOnly Expected,
    DateOnly Latest,
    int DaysFromNow,
    int WindowWidthDays,
    string Confidence,
    string Basis);

public record DailyGddPoint(
    DateOnly Date,
    double MaxC,
    double MinC,
    double Gdd,
    double Cumulative,
    TemperatureSource Source);

public record MethodologyInfo(
    string Model,
    string WeatherSource,
    int AnalogYears,
    string Notes);

/// <summary>Pure computation result produced by <see cref="IHarvestPredictor"/> — not persisted as-is;
/// <see cref="HarvestPrediction"/> stores the durable subset of it.</summary>
public record HarvestPredictionResult(
    DateOnly AsOf,
    bool AlreadyMature,
    GddSummary Gdd,
    GrowthStageInfo Stage,
    HarvestWindow Harvest,
    MethodologyInfo Methodology,
    IReadOnlyList<DailyGddPoint> DailyTrace);
