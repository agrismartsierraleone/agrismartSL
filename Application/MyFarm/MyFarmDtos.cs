using AgrismartAPI.Application.Diagnoses;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Diagnoses;
using AgrismartAPI.Domain.Farms;

namespace AgrismartAPI.Application.MyFarm;

// ---------------------------------------------------------------- enums shown to the app

/// <summary>Status shown on a crop card. Planned/growing/ready are worked out from the dates.</summary>
public enum FarmCropDisplayStatus
{
    /// <summary>No planting date yet, or it is in the future.</summary>
    Planned,
    Growing,

    /// <summary>Still in the field but past its expected harvest date.</summary>
    ReadyToHarvest,
    Harvested,
    Failed
}

public enum CalendarStageStatus
{
    Completed,
    InProgress,
    Upcoming,

    /// <summary>The stage window has passed but some of its tasks are still pending.</summary>
    Overdue
}

/// <summary>Values for PUT /farms/{farmId}/crops/{id}/status.</summary>
public enum FarmCropStatusChange
{
    Active,
    Failed
}

// ---------------------------------------------------------------- requests

public record GeoPointDto(double Latitude, double Longitude);

/// <summary>A crop plot to add to (or replace on) a farm.</summary>
/// <param name="CropId">Crop key from GET /farms/options (or GET /crops), e.g. "rice-lowland".</param>
/// <param name="PlotName">e.g. "Plot 1". Defaults to "Plot N" when adding.</param>
/// <param name="Variety">Seed variety, e.g. "NERICA 14".</param>
/// <param name="PlantingDate">yyyy-MM-dd. Leave empty while the crop is only planned.</param>
/// <param name="ExpectedHarvestDate">yyyy-MM-dd. Leave empty to have it estimated from the crop's typical days to maturity.</param>
/// <param name="PlotSize">Size in <paramref name="AreaUnit"/>.</param>
/// <param name="AreaUnit">hectare (default), acre or squareMeter.</param>
/// <param name="Latitude">Plot pin for the map; give with longitude.</param>
/// <param name="Longitude">Plot pin for the map; give with latitude.</param>
/// <param name="Boundary">Plot outline for the map (3 to 200 points), or omit.</param>
/// <param name="Notes">Free text, up to 1000 characters.</param>
/// <param name="GenerateActivities">
/// When true (default) the crop calendar's upcoming tasks (fertilizer doses, weeding, pest checks, harvest) are added
/// as pending activities. On edit they are regenerated only if the crop, planting date or harvest date changed.
/// </param>
public record FarmCropUpsertRequest(
    string CropId,
    string? PlotName = null,
    string? Variety = null,
    DateOnly? PlantingDate = null,
    DateOnly? ExpectedHarvestDate = null,
    double? PlotSize = null,
    AreaUnit AreaUnit = AreaUnit.Hectare,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<GeoPointDto>? Boundary = null,
    string? Notes = null,
    bool GenerateActivities = true);

/// <summary>Marks a crop harvested and, if a quantity is given, records the yield.</summary>
/// <param name="HarvestedOn">Defaults to today.</param>
/// <param name="Quantity">Amount harvested, e.g. 1200.</param>
/// <param name="Unit">e.g. "kg" or "bags". Required with quantity.</param>
public record HarvestCropRequest(DateOnly? HarvestedOn = null, double? Quantity = null, string? Unit = null, string? Notes = null);

public record CropStatusRequest(FarmCropStatusChange Status);

/// <param name="FarmCropId">The crop plot this task is for (from the crops list), or null for whole-farm work.</param>
/// <param name="Cost">Planned or actual spend in Leones (SLE).</param>
public record FarmActivityRequest(
    FarmActivityType Type,
    string Title,
    DateOnly DueDate,
    Guid? FarmCropId = null,
    string? Notes = null,
    decimal? Cost = null);

/// <param name="CompletedOn">Defaults to today.</param>
/// <param name="Cost">Actual spend in Leones; replaces the planned cost when given.</param>
public record CompleteActivityRequest(DateOnly? CompletedOn = null, decimal? Cost = null, string? Notes = null);

/// <summary>Farm pin and outline. Both are replaced; omit a field to clear it.</summary>
public record FarmMapRequest(double? Latitude = null, double? Longitude = null, IReadOnlyList<GeoPointDto>? Boundary = null);

// ---------------------------------------------------------------- responses

/// <summary>The header: "Green Valley Farm · 4.5 ha · Port Loko".</summary>
public record FarmHeaderDto(
    Guid Id,
    string Name,
    string? District,
    double? AreaHectares,
    double? Latitude,
    double? Longitude,
    int ActiveCropCount,
    double CultivatedHectares);

/// <summary>One crop card in "My Crops".</summary>
/// <param name="ProgressPercent">0-100: days since planting over days from planting to harvest.</param>
/// <param name="GrowthStage">e.g. "Tillering". Null when there is no planting date.</param>
/// <param name="ExpectedHarvestIsEstimate">True when the date was estimated from the crop catalogue.</param>
/// <param name="DaysToHarvest">Negative once the expected date has passed.</param>
/// <param name="ImageUrl">Farmer's own photo (root-relative), or null: show the app's default image for <paramref name="CropId"/>.</param>
public record FarmCropCardDto(
    Guid Id,
    Guid FarmId,
    string CropId,
    string CropName,
    string? LocalName,
    string? PlotName,
    string? Variety,
    double? PlotSizeHectares,
    DateOnly? PlantingDate,
    DateOnly? ExpectedHarvestDate,
    bool ExpectedHarvestIsEstimate,
    int? DaysToHarvest,
    FarmCropDisplayStatus Status,
    int ProgressPercent,
    string? GrowthStage,
    DateOnly? HarvestedOn,
    string? ImageUrl,
    GeoPointDto? Location,
    IReadOnlyList<GeoPointDto> Boundary,
    string? Notes,
    int PendingActivityCount,
    DateTimeOffset CreatedAt);

/// <summary>One step of the crop calendar strip (Land Prep → Harvesting).</summary>
/// <param name="Key">landPreparation, planting, fertilizing, weeding, pestControl or harvesting.</param>
/// <param name="KeyDate">The date to print under the icon.</param>
/// <param name="DatePrecision">"day" (print "12 Jun") or "month" (print "Jul").</param>
public record CalendarStageDto(
    string Key,
    string Name,
    FarmActivityType ActivityType,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateOnly? KeyDate,
    string DatePrecision,
    CalendarStageStatus Status,
    int ActivityCount,
    int CompletedActivityCount);

/// <param name="CurrentStageKey">The stage to highlight: the first one not completed. Null when all are done.</param>
/// <param name="Activities">All of this crop's activities, by due date. Empty in the overview.</param>
public record CropCalendarDto(
    Guid FarmCropId,
    string CropId,
    string CropName,
    string? PlotName,
    double? PlotSizeHectares,
    string? CurrentStageKey,
    IReadOnlyList<CalendarStageDto> Stages,
    IReadOnlyList<FarmActivityDto> Activities);

/// <param name="DueInDays">0 = today, 1 = tomorrow, negative = overdue (pending only).</param>
public record FarmActivityDto(
    Guid Id,
    Guid FarmId,
    Guid? FarmCropId,
    string? CropId,
    string? CropName,
    string? PlotName,
    FarmActivityType Type,
    string Title,
    string? Notes,
    DateOnly DueDate,
    int DueInDays,
    bool IsOverdue,
    FarmActivityStatus Status,
    DateOnly? CompletedOn,
    decimal? Cost,
    FarmActivitySource Source,
    DateTimeOffset CreatedAt);

/// <param name="ConfidencePercent">0-100, e.g. 92.</param>
/// <param name="Headline">e.g. "No major disease detected" or "Rice blast detected".</param>
public record CropHealthScanDto(
    Guid ScanId,
    string ImageUrl,
    DateTimeOffset ScannedAt,
    Guid? FarmCropId,
    string? CropName,
    string PlantName,
    string? DiseaseName,
    Severity Severity,
    bool IsHealthy,
    int ConfidencePercent,
    string Headline,
    string Summary,
    IReadOnlyList<TreatmentStepDto> TreatmentSteps);

/// <param name="LastScan">Latest scan for this farm, or (when the farm has none) the farmer's latest scan anywhere.</param>
/// <param name="LastScanIsFromThisFarm">False when <paramref name="LastScan"/> is the fallback.</param>
/// <param name="History">This farm's scans, newest first.</param>
public record CropHealthDto(
    CropHealthScanDto? LastScan,
    bool LastScanIsFromThisFarm,
    int TotalScans,
    IReadOnlyList<CropHealthScanDto> History);

/// <summary>Everything the "Crops" tab needs in one call.</summary>
public record FarmOverviewDto(
    FarmHeaderDto Farm,
    IReadOnlyList<FarmCropCardDto> Crops,
    CropCalendarDto? Calendar,
    IReadOnlyList<FarmActivityDto> UpcomingActivities,
    int PendingActivityCount,
    int OverdueActivityCount,
    CropHealthScanDto? CropHealth,
    bool CropHealthIsFromThisFarm);

public record FarmMapPlotDto(
    Guid FarmCropId,
    string CropId,
    string CropName,
    string? PlotName,
    FarmCropDisplayStatus Status,
    double? AreaHectares,
    GeoPointDto? Location,
    IReadOnlyList<GeoPointDto> Boundary);

/// <param name="Center">Farm pin, or null if no location has been set.</param>
public record FarmMapDto(
    Guid FarmId,
    string Name,
    string? District,
    GeoPointDto? Center,
    IReadOnlyList<GeoPointDto> Boundary,
    double? AreaHectares,
    IReadOnlyList<FarmMapPlotDto> Plots);

public record CropStatusCountsDto(int Total, int Planned, int Growing, int ReadyToHarvest, int Harvested, int Failed);

public record AreaByCropDto(string CropId, string CropName, double Hectares, double Percent);

public record ActivityCountsDto(int Total, int Pending, int Completed, int Skipped, int Overdue, int DueThisWeek, double CompletionRatePercent);

public record ActivityTypeCountDto(FarmActivityType Type, int Total, int Completed, decimal Cost);

/// <param name="Month">yyyy-MM.</param>
public record MonthlyActivityDto(string Month, int Scheduled, int Completed, decimal Cost);

public record YieldSummaryDto(string? CropId, string CropName, double Quantity, string? Unit, int Harvests);

public record HealthSummaryDto(int Scans, int Healthy, int Diseased, double HealthyRatePercent, DateTimeOffset? LastScanAt);

/// <summary>The "Analytics" tab.</summary>
/// <param name="TotalCost">Sum of activity costs in Leones (SLE).</param>
public record FarmAnalyticsDto(
    Guid FarmId,
    double? AreaHectares,
    double CultivatedHectares,
    double? LandUsePercent,
    int AverageGrowthProgressPercent,
    CropStatusCountsDto Crops,
    IReadOnlyList<AreaByCropDto> AreaByCrop,
    ActivityCountsDto Activities,
    IReadOnlyList<ActivityTypeCountDto> ActivitiesByType,
    IReadOnlyList<MonthlyActivityDto> Monthly,
    decimal TotalCost,
    string Currency,
    IReadOnlyList<YieldSummaryDto> Yields,
    HealthSummaryDto Health);

public record CropOptionDto(string Id, string Name, string? LocalName, int? TypicalDaysToMaturity);

/// <param name="StageKey">The crop-calendar stage this type belongs to, or null.</param>
public record ActivityTypeOptionDto(FarmActivityType Type, string Label, string? StageKey);

/// <summary>Lookup lists for the My Farm forms.</summary>
public record MyFarmOptionsDto(
    IReadOnlyList<string> Districts,
    IReadOnlyList<CropOptionDto> Crops,
    IReadOnlyList<ActivityTypeOptionDto> ActivityTypes,
    IReadOnlyList<string> AreaUnits,
    string Currency);
