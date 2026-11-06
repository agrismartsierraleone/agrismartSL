using System.ComponentModel;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Application.Jobs;
using AgrismartAPI.Application.Logistics;
using AgrismartAPI.Application.Orders;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;
using AgrismartAPI.Domain.Logistics;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Scans;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    IMarketRepository marketRepository,
    ISellerRepository sellerRepository,
    IOrderRepository orderRepository,
    IOptions<DeliveryOptions> deliveryOptions,
    DeliveryEstimateService deliveryEstimateService,
    JobAppService jobAppService,
    ILogger<AssistantTools> logger)
{
    private const int HistoryItems = 10;
    private const int MaxProductResults = 10;
    private const int MaxJobResults = 10;

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
            "Predicts the harvest date window for a crop from its planting date and the farm location. Always use this; never estimate dates yourself."),
        AIFunctionFactory.Create(SearchProductsAsync, "search_products",
            "Searches the AgriSmart marketplace for products, optionally near a district. Returns names, prices, units, sellers and seller locations."),
        AIFunctionFactory.Create(GetMyOrdersAsync, "get_my_orders",
            "Gets the user's own recent orders with their payment status, delivery status and delivery destination."),
        AIFunctionFactory.Create(GetMySellerEarningsAsync, "get_my_seller_earnings",
            "For sellers: gets the user's own earnings, split into received, confirmed and pending, plus best-selling products."),
        AIFunctionFactory.Create(EstimateDeliveryCost, "estimate_delivery_cost",
            "Estimates a delivery cost between two Sierra Leone districts for a weight in kilograms. Always use this; never estimate costs yourself."),
        AIFunctionFactory.Create(SearchJobsAsync, "search_jobs",
            "Searches open farm jobs, optionally by district or skill.")
    ];

    // ---------- search_products ----------

    public async Task<object> SearchProductsAsync(
        [Description("What to look for, e.g. \"rice\" or \"maize seed\".")] string query,
        [Description("Optional Sierra Leone district or town to search near.")] string? district = null,
        CancellationToken cancellationToken = default)
    {
        var words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return new ToolError("Ask the user what product they want.");
        }

        var nearby = SierraLeoneDistricts.Find(district);
        if (!string.IsNullOrWhiteSpace(district) && nearby is null)
        {
            return new ToolError($"'{district}' is not a known Sierra Leone district or town. Ask the user for their district.");
        }

        var products = await marketRepository.GetAllAsync(null, cancellationToken);
        var matches = products
            .Where(product => words.All(word =>
                product.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                || product.Category.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                || product.Description.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Select(product => (Product: product, SellerDistrict: SierraLeoneDistricts.Find(product.Seller.Location)?.Name))
            .OrderByDescending(match => nearby is not null && match.SellerDistrict == nearby.Name)
            .ThenBy(match => match.Product.Price)
            .Take(MaxProductResults)
            .Select(match => new ProductSearchResult(
                match.Product.Id, match.Product.Name, match.Product.Category.Name, match.Product.Price, match.Product.Unit,
                match.Product.Seller.Name, match.Product.Seller.Location, match.SellerDistrict is not null && match.SellerDistrict == nearby?.Name))
            .ToList();

        return new ProductSearchResults(matches, deliveryOptions.Value.Currency, matches.Count == 0 ? "No matching products were found." : null);
    }

    // ---------- get_my_orders ----------

    public async Task<MyOrdersResult> GetMyOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetAllForUserAsync(currentUser.UserId, cancellationToken);

        return new MyOrdersResult(
            orders.Take(HistoryItems).Select(order => new MyOrderInfo(
                order.Id,
                order.CreatedAt,
                order.TotalAmount,
                order.Items.Select(item => $"{item.Quantity} x {item.ProductName}").ToList(),
                order.PaymentMethod.ToString(),
                order.PaymentStatus.ToString(),
                order.Status.ToString(),
                order.HoldExpiresAt,
                order.DeliveryDistrict,
                order.ShippedAt,
                order.DeliveredAt)).ToList(),
            deliveryOptions.Value.Currency,
            orders.Count == 0 ? "This user has no orders." : null);
    }

    // ---------- get_my_seller_earnings ----------

    public async Task<object> GetMySellerEarningsAsync(
        [Description("How many past days to include, 1-366 (default 30).")] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var seller = await sellerRepository.GetByUserIdAsync(currentUser.UserId, cancellationToken);
        if (seller is null)
        {
            return new ToolError("This user has no seller profile, so there are no earnings to show.");
        }

        var orders = await orderRepository.GetAllAsync(cancellationToken);
        return SellerEarningsCalculator.Calculate(orders, seller.Id, Math.Clamp(days, 1, 366), DateTimeOffset.UtcNow);
    }

    // ---------- estimate_delivery_cost ----------

    public object EstimateDeliveryCost(
        [Description("Sierra Leone district or town the goods leave from.")] string fromDistrict,
        [Description("Sierra Leone district or town the goods go to.")] string toDistrict,
        [Description("Total weight in kilograms.")] double weightKg)
    {
        try
        {
            return deliveryEstimateService.Estimate(new DeliveryEstimateRequest(fromDistrict, toDistrict, weightKg));
        }
        catch (ValidationException exception)
        {
            return new ToolError(exception.Message, exception.Errors);
        }
        catch (ConflictException exception)
        {
            return new ToolError(exception.Message);
        }
    }

    // ---------- search_jobs ----------

    public async Task<object> SearchJobsAsync(
        [Description("Optional Sierra Leone district or town.")] string? district = null,
        [Description("Optional skill, e.g. \"harvesting\".")] string? skill = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobs = await jobAppService.GetOpenAsync(district, skill, cancellationToken);
            return jobs.Take(MaxJobResults)
                .Select(job => new JobSearchResult(job.Id, job.Title, job.District, job.WorkersNeeded, job.StartDate, job.Pay, job.Skills))
                .ToList();
        }
        catch (ValidationException exception)
        {
            return new ToolError(exception.Message, exception.Errors);
        }
    }

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

public record ProductSearchResult(Guid ProductId, string Name, string Category, decimal Price, string Unit, string Seller, string? SellerLocation, bool InRequestedDistrict);

public record ProductSearchResults(IReadOnlyList<ProductSearchResult> Products, string Currency, string? Note);

public record MyOrderInfo(
    Guid OrderId,
    DateTimeOffset PlacedAt,
    decimal Total,
    IReadOnlyList<string> Items,
    string PaymentMethod,
    string PaymentStatus,
    string DeliveryStatus,
    DateTimeOffset? UnpaidHoldExpiresAt,
    string? DeliveryDistrict,
    DateTimeOffset? ShippedAt,
    DateTimeOffset? DeliveredAt);

public record MyOrdersResult(IReadOnlyList<MyOrderInfo> Orders, string Currency, string? Note);

public record JobSearchResult(Guid JobId, string Title, string District, int WorkersNeeded, DateOnly? StartDate, string? Pay, IReadOnlyList<string> Skills);

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
