using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Application.Harvest;

public class HarvestAppService(
    ICropRepository cropRepository,
    IHarvestPredictionRepository harvestPredictionRepository,
    IHarvestPredictor predictor)
{
    public async Task<IReadOnlyList<CropSummaryDto>> GetCropsAsync(CancellationToken cancellationToken = default)
    {
        var crops = await cropRepository.GetAllAsync(cancellationToken);
        return crops.Select(HarvestMapper.ToDto).ToList();
    }

    public async Task<CropSummaryDto> GetCropAsync(string cropId, CancellationToken cancellationToken = default)
    {
        var crop = await cropRepository.GetByKeyAsync(cropId, cancellationToken)
                   ?? throw new NotFoundException($"No crop profile with id '{cropId}'.");
        return HarvestMapper.ToDto(crop);
    }

    public async Task<HarvestPredictionResponseDto> PredictAsync(
        HarvestPredictionRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        Validate(request);

        var crop = await cropRepository.GetByKeyAsync(request.CropId, cancellationToken)
                   ?? throw new NotFoundException($"Unknown crop '{request.CropId}'. Call GET /crops for the list.");

        var result = await predictor.PredictAsync(
            crop, request.Latitude, request.Longitude, request.PlantingDate, request.GddToMaturityOverride, cancellationToken);

        var prediction = HarvestPrediction.Create(
            userId, crop.Key, crop.Name, request.Latitude, request.Longitude, request.PlantingDate,
            result.AsOf, result.AlreadyMature, result.Gdd, result.Stage, result.Harvest, result.Methodology);

        await harvestPredictionRepository.AddAsync(prediction, cancellationToken);

        return HarvestMapper.ToDto(prediction, request.IncludeDailyTrace ? result.DailyTrace : null);
    }

    public async Task<IReadOnlyList<HarvestPredictionResponseDto>> GetHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var predictions = await harvestPredictionRepository.GetHistoryForUserAsync(userId, cancellationToken);
        return predictions.Select(p => HarvestMapper.ToDto(p, null)).ToList();
    }

    private static void Validate(HarvestPredictionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CropId))
        {
            throw new ValidationException("cropId is required. See GET /crops.");
        }

        if (request.Latitude is < -90 or > 90)
        {
            throw new ValidationException("latitude must be between -90 and 90.");
        }

        if (request.Longitude is < -180 or > 180)
        {
            throw new ValidationException("longitude must be between -180 and 180.");
        }

        if (request.PlantingDate == default)
        {
            throw new ValidationException("plantingDate is required (yyyy-MM-dd).");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.PlantingDate < today.AddYears(-3))
        {
            throw new ValidationException("plantingDate cannot be more than 3 years in the past.");
        }

        if (request.PlantingDate > today.AddYears(1))
        {
            throw new ValidationException("plantingDate cannot be more than 1 year in the future.");
        }

        if (request.GddToMaturityOverride is <= 0 or > 20000)
        {
            throw new ValidationException("gddToMaturityOverride must be between 1 and 20000.");
        }
    }
}
