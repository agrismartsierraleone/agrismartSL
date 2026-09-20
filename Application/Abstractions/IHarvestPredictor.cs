using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Application.Abstractions;

/// <summary>
/// Growing-degree-day harvest predictor with an analog-year ensemble for the part of the
/// season that has not happened yet.
/// </summary>
public interface IHarvestPredictor
{
    Task<HarvestPredictionResult> PredictAsync(
        CropProfile crop,
        double latitude,
        double longitude,
        DateOnly plantingDate,
        double? gddToMaturityOverride,
        CancellationToken cancellationToken = default);
}
