using System.ComponentModel;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;
using AgrismartAPI.Domain.Scans;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgrismartAPI.Application.Assistant;

/// <summary>
/// The functions the AI model may call. Each one is a thin adapter over EXISTING services and
/// repositories. The farmer is always the authenticated caller (<see cref="ICurrentUserService"/>):
/// no tool accepts a user/farmer id, so the model cannot read another farmer's data.
/// </summary>
public class AssistantTools(
    ICurrentUserService currentUser,
    IFarmRepository farmRepository,
    IHarvestPredictionRepository harvestPredictionRepository,
    IFertilizerRecommendationRepository fertilizerRecommendationRepository,
    IScanHistoryRepository scanHistoryRepository,
    IWeatherProvider weatherProvider,
    FertilizerAppService fertilizerAppService,
    ICropRepository cropRepository,
    IHarvestPredictor harvestPredictor,
    ILogger<AssistantTools> logger)
{
    private const int HistoryItems = 10;

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(GetFarmProfileAsync, "get_farm_profile",
            "Gets the farmer's farms: crops, planting dates, plot sizes and location (district, latitude/longitude). " +
            "Also returns crops and locations from the farmer's past harvest predictions and saved fertilizer plans."),
        AIFunctionFactory.Create(GetFarmRecordsAsync, "get_farm_records",
            "Gets the farmer's past records: fertilizer use, yields, reported problems and crop-photo diagnoses."),
        AIFunctionFactory.Create(GetWeatherForecastAsync, "get_weather_forecast",
            "Gets the daily minimum and maximum temperature forecast (no rainfall) for the farm location, or for the given coordinates."),
        AIFunctionFactory.Create(CalculateFertilizerAsync, "calculate_fertilizer",
            "Calculates how much fertilizer to buy and apply, with bags, schedule and cost. Always use this; never calculate amounts yourself."),
        AIFunctionFactory.Create(PredictHarvestAsync, "predict_harvest",
            "Predicts the harvest date window for a crop from its planting date and the farm location. Always use this; never estimate dates yourself.")
    ];

    // ---------- get_farm_profile ----------

    public async Task<FarmProfileResult> GetFarmProfileAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var farms = await farmRepository.GetFarmsForUserAsync(userId, cancellationToken);
        var predictions = await harvestPredictionRepository.GetHistoryForUserAsync(userId, cancellationToken);
        var (plans, _) = await fertilizerRecommendationRepository.SearchAsync(userId, null, null, 0, HistoryItems, cancellationToken);

        return new FarmProfileResult(
            farms.Select(farm => new FarmInfo(
                farm.Name,
                farm.District,
                farm.Latitude,
                farm.Longitude,
                farm.AreaHectares,
                farm.Crops.Select(crop => new FarmCropInfo(crop.CropId, crop.PlantingDate, crop.PlotSizeHectares)).ToList())).ToList(),
            predictions.Take(HistoryItems).Select(prediction => new PlantingFromHistory(
                prediction.CropId, prediction.CropName, prediction.PlantingDate, prediction.Latitude, prediction.Longitude,
                prediction.Harvest.Expected)).ToList(),
            plans.Select(plan => new FertilizerPlotFromHistory(
                plan.PlotLabel, plan.CropId, plan.CropName, Math.Round(plan.AreaHectares, 4), plan.CreatedAt)).ToList(),
            farms.Count == 0 && predictions.Count == 0 && plans.Count == 0
                ? "No farm details saved yet. Ask the farmer for their crop, farm size, planting date and location."
                : null);
    }

    // ---------- get_farm_records ----------

    public async Task<FarmRecordsResult> GetFarmRecordsAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var records = await farmRepository.GetRecordsForUserAsync(userId, HistoryItems * 2, cancellationToken);
        var (plans, _) = await fertilizerRecommendationRepository.SearchAsync(userId, null, null, 0, HistoryItems, cancellationToken);
        var scans = await scanHistoryRepository.GetAllForUserAsync(userId, cancellationToken);

        return new FarmRecordsResult(
            records.Select(record => new FarmRecordInfo(
                record.Type.ToString(), record.CropId, record.RecordDate, record.Quantity, record.Unit, record.Notes)).ToList(),
            plans.Select(plan => new FertilizerPlanFromHistory(
                plan.CropName, plan.PlotLabel, Math.Round(plan.AreaHectares, 4), plan.TotalEstimatedCost, plan.Currency, plan.CreatedAt)).ToList(),
            // Treatment steps are deliberately left out: they can contain chemical doses the assistant must not pass on.
            scans.Take(HistoryItems).Select(ToProblemInfo).ToList());
    }

    private static CropProblemFromScan ToProblemInfo(PlantScan scan) => new(
        scan.ScannedAt,
        scan.Diagnosis.PlantName,
        scan.Diagnosis.DiseaseName,
        Math.Round(scan.Diagnosis.Confidence, 2),
        scan.Diagnosis.Severity.ToString(),
        scan.Diagnosis.Summary);

    // ---------- get_weather_forecast ----------

    public async Task<object> GetWeatherForecastAsync(
        [Description("Latitude. Omit to use the farm location.")] double? latitude = null,
        [Description("Longitude. Omit to use the farm location.")] double? longitude = null,
        [Description("Number of forecast days, 1-16 (default 7).")] int days = 7,
        CancellationToken cancellationToken = default)
    {
        var location = await ResolveLocationAsync(latitude, longitude, cancellationToken);
        if (location is null)
        {
            return new ToolError("No farm location is saved. Ask the farmer where the farm is (town or district), or give latitude and longitude.");
        }

        var (lat, lon, source) = location.Value;
        try
        {
            var forecast = await weatherProvider.GetRecentAndForecastAsync(lat, lon, 0, Math.Clamp(days, 1, 16), cancellationToken);
            return new WeatherForecastResult(
                lat, lon, source,
                forecast.Select(day => new DailyWeather(day.Date, Math.Round(day.MinC, 1), Math.Round(day.MaxC, 1))).ToList(),
                "Temperatures only. No rainfall data is available.");
        }
        catch (ExternalServiceException exception)
        {
            logger.LogWarning(exception, "Assistant weather tool failed");
            return new ToolError("The weather service is not available right now.");
        }
    }

    // ---------- calculate_fertilizer ----------

    public async Task<object> CalculateFertilizerAsync(
        [Description("Fertilizer crop id, e.g. \"maize\", \"rice-lowland\", \"cassava\". An unknown id returns the list of valid ids.")] string cropId,
        [Description("Soil fertility as the farmer describes it: \"low\", \"medium\" or \"high\". Ask the farmer if unknown.")] string soilFertility,
        [Description("Field size in hectares. Omit to use the plot size saved for this crop on the farmer's farm.")] double? areaHectares = null,
        [Description("Optional target yield in tonnes per hectare.")] double? targetYieldTPerHa = null,
        [Description("Optional fertilizer product ids the farmer already has, e.g. [\"npk-15-15-15\", \"urea\"]. Omit to let the calculator choose.")] string[]? fertilizerProductIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<SoilFertility>(soilFertility, ignoreCase: true, out var fertility) || !Enum.IsDefined(fertility))
        {
            return new ToolError("soilFertility must be \"low\", \"medium\" or \"high\". Ask the farmer how fertile the soil is.");
        }

        areaHectares ??= await FindPlotSizeAsync(cropId, cancellationToken);
        if (areaHectares is null)
        {
            return new ToolError("The field size is not known. Ask the farmer how big the field is.");
        }

        var request = new FertilizerCalculationRequest(
            cropId?.Trim().ToLowerInvariant() ?? "",
            areaHectares,
            AreaUnit.Hectare,
            Soil: new SoilInputDto(SoilInputMode.Simple, fertility),
            TargetYieldTPerHa: targetYieldTPerHa,
            FertilizerProductIds: fertilizerProductIds);

        try
        {
            return await fertilizerAppService.CalculateAsync(request, cancellationToken);
        }
        catch (ValidationException exception)
        {
            if (exception.Errors?.ContainsKey("cropId") == true)
            {
                var crops = await fertilizerAppService.GetCropsAsync(cancellationToken);
                return new ToolError($"Unknown crop '{cropId}'.", exception.Errors, crops.Select(crop => crop.Id).ToList());
            }

            return new ToolError(exception.Message, exception.Errors);
        }
    }

    // ---------- predict_harvest ----------

    public async Task<object> PredictHarvestAsync(
        [Description("Harvest crop id, e.g. \"maize\", \"rice-lowland\", \"rice-upland\", \"cassava\". An unknown id returns the list of valid ids.")] string cropId,
        [Description("Planting date as yyyy-MM-dd. Omit to use the planting date saved for this crop on the farmer's farm.")] string? plantingDate = null,
        [Description("Latitude. Omit to use the farm location.")] double? latitude = null,
        [Description("Longitude. Omit to use the farm location.")] double? longitude = null,
        CancellationToken cancellationToken = default)
    {
        var crop = await cropRepository.GetByKeyAsync(cropId?.Trim().ToLowerInvariant() ?? "", cancellationToken);
        if (crop is null)
        {
            var crops = await cropRepository.GetAllAsync(cancellationToken);
            return new ToolError($"Unknown crop '{cropId}'.", ValidCropIds: crops.Select(c => c.Key).ToList());
        }

        DateOnly planted;
        if (plantingDate is not null)
        {
            if (!DateOnly.TryParseExact(plantingDate, "yyyy-MM-dd", out planted))
            {
                return new ToolError("plantingDate must be in yyyy-MM-dd format.");
            }
        }
        else if (await FindPlantingDateAsync(crop.Key, cancellationToken) is { } saved)
        {
            planted = saved;
        }
        else
        {
            return new ToolError("The planting date is not known. Ask the farmer when they planted.");
        }

        // Same limits as HarvestAppService (POST /harvest/predict).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (planted < today.AddYears(-3) || planted > today.AddYears(1))
        {
            return new ToolError("plantingDate must be within the last 3 years and at most 1 year in the future.");
        }

        var location = await ResolveLocationAsync(latitude, longitude, cancellationToken);
        if (location is null)
        {
            return new ToolError("No farm location is saved. Ask the farmer where the farm is (town or district), or give latitude and longitude.");
        }

        var (lat, lon, source) = location.Value;
        try
        {
            // The predictor is called directly (not HarvestAppService) so assistant questions do not add rows to
            // the farmer's /harvest/history.
            var result = await harvestPredictor.PredictAsync(crop, lat, lon, planted, null, cancellationToken);
            return new HarvestPredictionToolResult(
                crop.Key, crop.Name, planted, lat, lon, source, result.AsOf, result.AlreadyMature,
                result.Harvest, result.Stage, Math.Round(result.Gdd.PercentComplete, 1), result.Methodology.Notes);
        }
        catch (ExternalServiceException exception)
        {
            logger.LogWarning(exception, "Assistant harvest tool failed");
            return new ToolError("The weather data needed for the harvest prediction is not available right now.");
        }
    }

    // ---------- Helpers ----------

    /// <summary>Explicit coordinates win; then the first farm with coordinates; then the latest harvest-prediction location.</summary>
    private async Task<(double Latitude, double Longitude, string Source)?> ResolveLocationAsync(
        double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        if (latitude is { } lat && longitude is { } lon && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
        {
            return (lat, lon, "given");
        }

        var userId = currentUser.UserId;
        var farms = await farmRepository.GetFarmsForUserAsync(userId, cancellationToken);
        if (farms.FirstOrDefault(farm => farm.Latitude.HasValue && farm.Longitude.HasValue) is { } located)
        {
            return (located.Latitude!.Value, located.Longitude!.Value, $"farm '{located.Name}'");
        }

        var predictions = await harvestPredictionRepository.GetHistoryForUserAsync(userId, cancellationToken);
        if (predictions.FirstOrDefault() is { } latest)
        {
            return (latest.Latitude, latest.Longitude, "last harvest prediction location");
        }

        return null;
    }

    private async Task<double?> FindPlotSizeAsync(string? cropId, CancellationToken cancellationToken)
    {
        var farms = await farmRepository.GetFarmsForUserAsync(currentUser.UserId, cancellationToken);
        var key = cropId?.Trim().ToLowerInvariant();
        return farms.SelectMany(farm => farm.Crops)
            .Where(crop => crop.PlotSizeHectares.HasValue && key is not null && MatchesCrop(crop.CropId, key))
            .Select(crop => crop.PlotSizeHectares)
            .FirstOrDefault();
    }

    private async Task<DateOnly?> FindPlantingDateAsync(string cropKey, CancellationToken cancellationToken)
    {
        var farms = await farmRepository.GetFarmsForUserAsync(currentUser.UserId, cancellationToken);
        return farms.SelectMany(farm => farm.Crops)
            .Where(crop => crop.PlantingDate.HasValue && MatchesCrop(crop.CropId, cropKey))
            .OrderByDescending(crop => crop.PlantingDate)
            .Select(crop => crop.PlantingDate)
            .FirstOrDefault();
    }

    /// <summary>The harvest and fertilizer catalogues use slightly different keys (e.g. "rice" vs "rice-lowland").</summary>
    private static bool MatchesCrop(string savedCropId, string key) =>
        savedCropId == key || savedCropId.StartsWith(key + "-") || key.StartsWith(savedCropId + "-");
}

// ---------- Tool results (serialized to JSON for the model) ----------

public record ToolError(string Error, IReadOnlyDictionary<string, string[]>? Errors = null, IReadOnlyList<string>? ValidCropIds = null);

public record FarmCropInfo(string CropId, DateOnly? PlantingDate, double? PlotSizeHectares);

public record FarmInfo(string Name, string? District, double? Latitude, double? Longitude, double? AreaHectares, IReadOnlyList<FarmCropInfo> Crops);

public record PlantingFromHistory(string CropId, string CropName, DateOnly PlantingDate, double Latitude, double Longitude, DateOnly ExpectedHarvest);

public record FertilizerPlotFromHistory(string? PlotLabel, string CropId, string CropName, double AreaHectares, DateTimeOffset SavedAt);

public record FarmProfileResult(
    IReadOnlyList<FarmInfo> Farms,
    IReadOnlyList<PlantingFromHistory> PastHarvestPredictions,
    IReadOnlyList<FertilizerPlotFromHistory> SavedFertilizerPlots,
    string? Note);

public record FarmRecordInfo(string Type, string? CropId, DateOnly Date, double? Quantity, string? Unit, string? Notes);

public record FertilizerPlanFromHistory(string CropName, string? PlotLabel, double AreaHectares, decimal TotalEstimatedCost, string Currency, DateTimeOffset SavedAt);

public record CropProblemFromScan(DateTimeOffset ScannedAt, string PlantName, string? DiseaseName, double Confidence, string Severity, string Summary);

public record FarmRecordsResult(
    IReadOnlyList<FarmRecordInfo> Records,
    IReadOnlyList<FertilizerPlanFromHistory> SavedFertilizerPlans,
    IReadOnlyList<CropProblemFromScan> CropPhotoDiagnoses);

public record DailyWeather(DateOnly Date, double MinC, double MaxC);

public record WeatherForecastResult(double Latitude, double Longitude, string LocationSource, IReadOnlyList<DailyWeather> Days, string Note);

public record HarvestPredictionToolResult(
    string CropId,
    string CropName,
    DateOnly PlantingDate,
    double Latitude,
    double Longitude,
    string LocationSource,
    DateOnly AsOf,
    bool AlreadyMature,
    HarvestWindow Harvest,
    GrowthStageInfo Stage,
    double PercentComplete,
    string Notes);
