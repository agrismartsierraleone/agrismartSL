using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Assistant;

/// <summary>A crop planted on a <see cref="Farm"/>.</summary>
public class FarmCrop : Entity
{
    public Guid FarmId { get; private init; }

    /// <summary>Crop key, e.g. "cassava" or "rice-lowland".</summary>
    public string CropId { get; private init; }

    public DateOnly? PlantingDate { get; private init; }
    public double? PlotSizeHectares { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private FarmCrop(Guid farmId, string cropId, DateOnly? plantingDate, double? plotSizeHectares)
    {
        FarmId = farmId;
        CropId = cropId;
        PlantingDate = plantingDate;
        PlotSizeHectares = plotSizeHectares;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private FarmCrop()
    {
        CropId = null!;
    }

    internal static FarmCrop Create(Guid farmId, string cropId, DateOnly? plantingDate, double? plotSizeHectares)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new ArgumentException("Crop id is required.", nameof(cropId));
        }

        if (plotSizeHectares is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plotSizeHectares), "Plot size must be positive.");
        }

        return new FarmCrop(farmId, cropId.Trim().ToLowerInvariant(), plantingDate, plotSizeHectares);
    }
}
