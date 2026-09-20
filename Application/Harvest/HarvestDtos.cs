namespace AgrismartAPI.Application.Harvest;

public record GrowthStageDto(string Name, double StartFraction);

public record CropSummaryDto(
    string Id,
    string Name,
    double BaseTemperatureC,
    double UpperTemperatureC,
    double GddToMaturity,
    int TypicalDaysToMaturity,
    IReadOnlyList<GrowthStageDto> Stages);

/// <summary>Crop key, latitude/longitude and planting date needed to run a harvest prediction.</summary>
public record HarvestPredictionRequest(
    string CropId,
    double Latitude,
    double Longitude,
    DateOnly PlantingDate,
    bool IncludeDailyTrace = false,
    double? GddToMaturityOverride = null);

public record CoordinatesDto(double Latitude, double Longitude);

public record GddSummaryDto(
    double Accumulated,
    double Required,
    double Remaining,
    double PercentComplete,
    double BaseTemperatureC,
    double UpperTemperatureC,
    int DaysObserved,
    int DaysForecast);

public record GrowthStageInfoDto(string Current, string? Next, DateOnly? NextStageStarts);

public record HarvestWindowDto(
    DateOnly Earliest,
    DateOnly Expected,
    DateOnly Latest,
    int DaysFromNow,
    int WindowWidthDays,
    string Confidence,
    string Basis);

public record DailyGddPointDto(DateOnly Date, double MaxC, double MinC, double Gdd, double Cumulative, string Source);

public record MethodologyInfoDto(string Model, string WeatherSource, int AnalogYears, string Notes);

public record HarvestPredictionResponseDto(
    Guid Id,
    string CropId,
    string CropName,
    CoordinatesDto Location,
    DateOnly PlantingDate,
    DateOnly AsOf,
    bool AlreadyMature,
    GddSummaryDto Gdd,
    GrowthStageInfoDto Stage,
    HarvestWindowDto Harvest,
    MethodologyInfoDto Methodology,
    IReadOnlyList<DailyGddPointDto>? DailyTrace,
    DateTimeOffset CreatedAt);
