using System.Text.Json.Serialization;
using AgrismartAPI.Domain.Fertilizer;

namespace AgrismartAPI.Application.Fertilizer;

/// <summary>Unit the farm/plot size is given in. Everything is converted to hectares internally.</summary>
public enum AreaUnit
{
    Hectare,
    Acre,
    SquareMeter,

    /// <summary>Rectangular plot: use lengthM × widthM instead of area.</summary>
    PlotDimensions
}

/// <summary>How the soil condition is described.</summary>
public enum SoilInputMode
{
    /// <summary>A farmer's judgement: Low / Medium / High fertility.</summary>
    Simple,

    /// <summary>Laboratory soil-test values for N, P and K.</summary>
    SoilTest
}

public enum SoilFertility
{
    Low,
    Medium,
    High
}

public enum SoilType
{
    Sandy,
    Loamy,
    Clay
}

public enum FertilizerSelectionMode
{
    /// <summary>Only the products listed in fertilizerProductIds were used.</summary>
    FarmerSelected,

    /// <summary>No products were given; the API used the standard recommendation (NPK 15-15-15 + Urea by default).</summary>
    Automatic
}

// ---------- Reference data ----------

/// <summary>Kilograms of each primary nutrient. P and K are expressed as oxides (P2O5, K2O), as on fertilizer bags.</summary>
public record NutrientAmountsDto(
    [property: JsonPropertyName("n")] double N,
    [property: JsonPropertyName("p2o5")] double P2O5,
    [property: JsonPropertyName("k2o")] double K2O);

public record FertilizerSplitDto(
    string Stage,
    int DaysAfterPlanting,
    string Timing,
    double NitrogenPercent,
    double PhosphatePercent,
    double PotashPercent);

/// <summary>A crop with its nutrient demand. String fields are "" and plantsPerHectare is 0 when not known.</summary>
public record FertilizerCropDto(
    string Id,
    string Name,
    string LocalName,
    NutrientAmountsDto NutrientRequirementKgPerHa,
    double ReferenceYieldTPerHa,
    double MaxRealisticYieldTPerHa,
    string YieldBasis,
    int PlantsPerHectare,
    bool IsPerennial,
    string Notes,
    IReadOnlyList<FertilizerSplitDto> Schedule);

/// <summary>Admin create/update body for a crop. <c>id</c> is required on POST and ignored on PUT.</summary>
public record FertilizerCropRequest(
    string? Id,
    string Name,
    string? LocalName,
    double NitrogenKgPerHa,
    double PhosphateKgPerHa,
    double PotashKgPerHa,
    double ReferenceYieldTPerHa,
    double MaxRealisticYieldTPerHa,
    string? YieldBasis,
    int? PlantsPerHectare,
    bool IsPerennial,
    string? Notes,
    IReadOnlyList<FertilizerSplitDto> Schedule);

/// <summary>A fertilizer product. Optional percentages are 0 and pricePerBag is 0 (hasPrice=false) when not known.</summary>
public record FertilizerProductDto(
    string Id,
    string Name,
    FertilizerProductType Type,
    double NitrogenPercent,
    double PhosphatePercent,
    double PotashPercent,
    double SulphurPercent,
    double ZincPercent,
    double BagSizeKg,
    decimal PricePerBag,
    bool HasPrice,
    string Currency,
    string Notes);

/// <summary>Admin create/update body for a product. <c>id</c> is required on POST and ignored on PUT.</summary>
public record FertilizerProductRequest(
    string? Id,
    string Name,
    FertilizerProductType Type,
    double NitrogenPercent,
    double PhosphatePercent,
    double PotashPercent,
    double? SulphurPercent,
    double? ZincPercent,
    double? BagSizeKg,
    decimal? PricePerBag,
    string? Notes);

// ---------- Calculation request ----------

/// <summary>
/// Soil condition, in one of two modes. If <c>mode</c> is omitted it is inferred: any soil-test value
/// present means soilTest, otherwise simple.
/// </summary>
/// <param name="Mode">simple or soilTest.</param>
/// <param name="Fertility">Simple mode (required): low, medium or high.</param>
/// <param name="SoilType">Optional in both modes: sandy, loamy or clay.</param>
/// <param name="Ph">Optional in both modes, 3.0-10.0 (1:2.5 soil:water).</param>
/// <param name="NitrogenTotalPercent">Soil-test mode: total N in % (Kjeldahl).</param>
/// <param name="PhosphorusBray1MgPerKg">Soil-test mode: available P in mg/kg (Bray-1 extraction).</param>
/// <param name="PotassiumCmolPerKg">Soil-test mode: exchangeable K in cmol(+)/kg (1 M ammonium acetate, pH 7).</param>
public record SoilInputDto(
    SoilInputMode? Mode = null,
    SoilFertility? Fertility = null,
    SoilType? SoilType = null,
    double? Ph = null,
    double? NitrogenTotalPercent = null,
    double? PhosphorusBray1MgPerKg = null,
    double? PotassiumCmolPerKg = null);

/// <summary>Overrides a product's stored price for this one calculation.</summary>
public record ProductPriceDto(string ProductId, decimal PricePerBag);

/// <summary>Inputs for a fertilizer calculation.</summary>
/// <param name="CropId">Crop key from GET /fertilizer/crops, e.g. "maize".</param>
/// <param name="Area">Farm/plot size in <paramref name="AreaUnit"/>. Not used when areaUnit is plotDimensions.</param>
/// <param name="AreaUnit">hectare (default), acre, squareMeter or plotDimensions.</param>
/// <param name="LengthM">Plot length in metres (plotDimensions only).</param>
/// <param name="WidthM">Plot width in metres (plotDimensions only).</param>
/// <param name="Soil">Soil condition (required).</param>
/// <param name="TargetYieldTPerHa">Optional target yield; defaults to the crop's reference yield and is capped at its max realistic yield.</param>
/// <param name="FertilizerProductIds">Products the farmer has or prefers. Omit or send [] to let the API choose.</param>
/// <param name="CustomPrices">Optional per-bag prices that override stored prices.</param>
/// <param name="PlotLabel">Optional plot/farm name. Only stored when saving a recommendation.</param>
public record FertilizerCalculationRequest(
    string CropId,
    double? Area,
    AreaUnit AreaUnit = AreaUnit.Hectare,
    double? LengthM = null,
    double? WidthM = null,
    SoilInputDto? Soil = null,
    double? TargetYieldTPerHa = null,
    IReadOnlyList<string>? FertilizerProductIds = null,
    IReadOnlyList<ProductPriceDto>? CustomPrices = null,
    string? PlotLabel = null);

// ---------- Calculation response ----------

public record SoilAdjustmentDto(
    SoilInputMode Mode,
    SoilFertility NitrogenClass,
    SoilFertility PhosphorusClass,
    SoilFertility PotassiumClass,
    double NitrogenFactor,
    double PhosphateFactor,
    double PotashFactor);

/// <summary>One product to buy. estimatedCost is 0 and hasPrice is false when no price is known.</summary>
public record ProductQuantityDto(
    string ProductId,
    string Name,
    FertilizerProductType Type,
    double QuantityKg,
    double BagSizeKg,
    double Bags,
    double Bags50Kg,
    decimal PricePerBag,
    bool HasPrice,
    decimal EstimatedCost,
    NutrientAmountsDto NutrientsSupplied);

/// <summary>A product's share of one application. gramsPerPlant is 0 and perPlantHint "" when plant spacing is unknown.</summary>
public record ScheduleProductDto(
    string ProductId,
    string Name,
    double QuantityKg,
    double GramsPerPlant,
    string PerPlantHint);

public record ScheduleStageDto(
    string Stage,
    int DaysAfterPlanting,
    string Timing,
    NutrientAmountsDto Nutrients,
    IReadOnlyList<ScheduleProductDto> Products);

public record FertilizerCalculationResponseDto(
    string CropId,
    string Crop,
    double AreaHectares,
    double TargetYieldTPerHa,
    bool TargetYieldCapped,
    string YieldBasis,
    SoilAdjustmentDto SoilAdjustment,
    NutrientAmountsDto NutrientRequirementPerHaKg,
    NutrientAmountsDto NutrientRequirementKg,
    FertilizerSelectionMode SelectionMode,
    IReadOnlyList<ProductQuantityDto> Products,
    NutrientAmountsDto NutrientsSupplied,
    NutrientAmountsDto NutrientBalanceKg,
    IReadOnlyList<ScheduleStageDto> Schedule,
    int PlantCount,
    decimal TotalEstimatedCost,
    bool CostComplete,
    string Currency,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Notes);

// ---------- Saved history ----------

public record FertilizerRecommendationSummaryDto(
    Guid Id,
    string CropId,
    string CropName,
    string PlotLabel,
    double AreaHectares,
    decimal TotalEstimatedCost,
    string Currency,
    DateTimeOffset CreatedAt);

public record FertilizerRecommendationDto(
    Guid Id,
    Guid UserId,
    string CropId,
    string CropName,
    string PlotLabel,
    double AreaHectares,
    DateTimeOffset CreatedAt,
    FertilizerCalculationRequest Inputs,
    FertilizerCalculationResponseDto Result);

/// <summary>History filters. <c>userId</c> is honoured for admins only.</summary>
public record FertilizerRecommendationQuery(
    string? CropId = null,
    string? PlotLabel = null,
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 20);
