using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Farms;

/// <summary>
/// Links a plant-health scan (the existing PlantScans table, left unchanged) to the farm and, optionally,
/// the crop plot it was taken on. Powers the "Crop Health" card.
/// </summary>
public class FarmScan : Entity
{
    public Guid UserId { get; private init; }
    public Guid FarmId { get; private init; }
    public Guid? FarmCropId { get; private init; }
    public Guid PlantScanId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private FarmScan(Guid userId, Guid farmId, Guid? farmCropId, Guid plantScanId)
    {
        UserId = userId;
        FarmId = farmId;
        FarmCropId = farmCropId;
        PlantScanId = plantScanId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private FarmScan()
    {
    }

    public static FarmScan Create(Guid userId, Guid farmId, Guid? farmCropId, Guid plantScanId)
    {
        if (userId == Guid.Empty || farmId == Guid.Empty || plantScanId == Guid.Empty)
        {
            throw new ArgumentException("User, farm and scan ids are required.");
        }

        return new FarmScan(userId, farmId, farmCropId, plantScanId);
    }
}
