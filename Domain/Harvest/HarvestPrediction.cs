using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Harvest;

/// <summary>
/// A saved record of one harvest prediction run — the durable subset of a
/// <see cref="HarvestPredictionResult"/> (the verbose day-by-day trace is not persisted).
/// </summary>
public class HarvestPrediction : Entity
{
    /// <summary>Who asked for this prediction. Null when the caller was not authenticated.</summary>
    public Guid? UserId { get; private init; }

    /// <summary>Crop key at the time of prediction, e.g. "maize".</summary>
    public string CropId { get; private init; }

    /// <summary>Crop display name, snapshotted so history reads correctly even if the catalog changes later.</summary>
    public string CropName { get; private init; }

    public double Latitude { get; private init; }
    public double Longitude { get; private init; }
    public DateOnly PlantingDate { get; private init; }
    public DateOnly AsOf { get; private init; }
    public bool AlreadyMature { get; private init; }
    public GddSummary Gdd { get; private init; }
    public GrowthStageInfo Stage { get; private init; }
    public HarvestWindow Harvest { get; private init; }
    public MethodologyInfo Methodology { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private HarvestPrediction(
        Guid? userId,
        string cropId,
        string cropName,
        double latitude,
        double longitude,
        DateOnly plantingDate,
        DateOnly asOf,
        bool alreadyMature,
        GddSummary gdd,
        GrowthStageInfo stage,
        HarvestWindow harvest,
        MethodologyInfo methodology)
    {
        UserId = userId;
        CropId = cropId;
        CropName = cropName;
        Latitude = latitude;
        Longitude = longitude;
        PlantingDate = plantingDate;
        AsOf = asOf;
        AlreadyMature = alreadyMature;
        Gdd = gdd;
        Stage = stage;
        Harvest = harvest;
        Methodology = methodology;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private HarvestPrediction()
    {
        CropId = null!;
        CropName = null!;
        Gdd = null!;
        Stage = null!;
        Harvest = null!;
        Methodology = null!;
    }

    public static HarvestPrediction Create(
        Guid? userId,
        string cropId,
        string cropName,
        double latitude,
        double longitude,
        DateOnly plantingDate,
        DateOnly asOf,
        bool alreadyMature,
        GddSummary gdd,
        GrowthStageInfo stage,
        HarvestWindow harvest,
        MethodologyInfo methodology)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new ArgumentException("Crop id is required.", nameof(cropId));
        }

        return new HarvestPrediction(
            userId, cropId, cropName, latitude, longitude, plantingDate,
            asOf, alreadyMature, gdd, stage, harvest, methodology);
    }
}
