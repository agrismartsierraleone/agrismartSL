using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Application.Harvest;

public static class HarvestMapper
{
    public static CropSummaryDto ToDto(CropProfile crop) => new(
        crop.Key,
        crop.Name,
        crop.BaseTemperatureC,
        crop.UpperTemperatureC,
        crop.GddToMaturity,
        crop.TypicalDaysToMaturity,
        crop.Stages.Select(s => new GrowthStageDto(s.Name, s.StartFraction)).ToList());

    public static HarvestPredictionResponseDto ToDto(HarvestPrediction prediction, IReadOnlyList<DailyGddPoint>? dailyTrace) => new(
        prediction.Id,
        prediction.CropId,
        prediction.CropName,
        new CoordinatesDto(prediction.Latitude, prediction.Longitude),
        prediction.PlantingDate,
        prediction.AsOf,
        prediction.AlreadyMature,
        ToDto(prediction.Gdd),
        ToDto(prediction.Stage),
        ToDto(prediction.Harvest),
        ToDto(prediction.Methodology),
        dailyTrace?.Select(ToDto).ToList(),
        prediction.CreatedAt);

    private static GddSummaryDto ToDto(GddSummary gdd) => new(
        gdd.Accumulated, gdd.Required, gdd.Remaining, gdd.PercentComplete,
        gdd.BaseTemperatureC, gdd.UpperTemperatureC, gdd.DaysObserved, gdd.DaysForecast);

    private static GrowthStageInfoDto ToDto(GrowthStageInfo stage) => new(stage.Current, stage.Next, stage.NextStageStarts);

    private static HarvestWindowDto ToDto(HarvestWindow harvest) => new(
        harvest.Earliest, harvest.Expected, harvest.Latest,
        harvest.DaysFromNow, harvest.WindowWidthDays, harvest.Confidence, harvest.Basis);

    private static MethodologyInfoDto ToDto(MethodologyInfo methodology) => new(
        methodology.Model, methodology.WeatherSource, methodology.AnalogYears, methodology.Notes);

    private static DailyGddPointDto ToDto(DailyGddPoint point) => new(
        point.Date, point.MaxC, point.MinC, point.Gdd, point.Cumulative, point.Source.ToString());
}
