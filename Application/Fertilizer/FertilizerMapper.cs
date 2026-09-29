using AgrismartAPI.Domain.Fertilizer;

namespace AgrismartAPI.Application.Fertilizer;

public static class FertilizerMapper
{
    public static FertilizerCropDto ToDto(FertilizerCropProfile crop) => new(
        crop.Key,
        crop.Name,
        crop.LocalName ?? "",
        new NutrientAmountsDto(crop.NitrogenKgPerHa, crop.PhosphateKgPerHa, crop.PotashKgPerHa),
        crop.ReferenceYieldTPerHa,
        crop.MaxRealisticYieldTPerHa,
        crop.YieldBasis,
        crop.PlantsPerHectare ?? 0,
        crop.IsPerennial,
        crop.Notes ?? "",
        crop.Schedule.Select(ToDto).ToList());

    public static FertilizerSplitDto ToDto(FertilizerSplit split) => new(
        split.Stage, split.DaysAfterPlanting, split.Timing, split.NitrogenPercent, split.PhosphatePercent, split.PotashPercent);

    public static FertilizerSplit ToDomain(FertilizerSplitDto split) => new(
        split.Stage?.Trim() ?? "", split.DaysAfterPlanting, split.Timing?.Trim() ?? "",
        split.NitrogenPercent, split.PhosphatePercent, split.PotashPercent);

    public static FertilizerProductDto ToDto(FertilizerProduct product, string currency) => new(
        product.Key,
        product.Name,
        product.Type,
        product.NitrogenPercent,
        product.PhosphatePercent,
        product.PotashPercent,
        product.SulphurPercent ?? 0,
        product.ZincPercent ?? 0,
        product.BagSizeKg,
        product.PricePerBag ?? 0m,
        product.PricePerBag is not null,
        currency,
        product.Notes ?? "");

    public static FertilizerRecommendationSummaryDto ToSummaryDto(FertilizerRecommendation recommendation) => new(
        recommendation.Id,
        recommendation.CropId,
        recommendation.CropName,
        recommendation.PlotLabel ?? "",
        recommendation.AreaHectares,
        recommendation.TotalEstimatedCost,
        recommendation.Currency,
        recommendation.CreatedAt);
}
