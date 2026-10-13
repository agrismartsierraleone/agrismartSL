using AgrismartAPI.Domain.Common;
using AgrismartAPI.Domain.Farms;

namespace AgrismartAPI.Domain.Assistant;

/// <summary>A crop planted on a <see cref="Farm"/>.</summary>
public class FarmCrop : Entity
{
    public const int MaxPlotNameLength = 60;
    public const int MaxVarietyLength = 60;
    public const int MaxNotesLength = 1000;

    public Guid FarmId { get; private init; }

    /// <summary>Crop key, e.g. "cassava" or "rice-lowland".</summary>
    public string CropId { get; private set; }

    public DateOnly? PlantingDate { get; private set; }
    public double? PlotSizeHectares { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Plot label shown to the farmer, e.g. "Plot 1".</summary>
    public string? PlotName { get; private set; }

    /// <summary>Seed variety, e.g. "NERICA 14" or "TME 419".</summary>
    public string? Variety { get; private set; }

    /// <summary>Farmer-supplied harvest date. Null means "estimate it from the crop catalogue".</summary>
    public DateOnly? ExpectedHarvestDate { get; private set; }

    public FarmCropStatus Status { get; private set; }
    public DateOnly? HarvestedOn { get; private set; }

    /// <summary>Root-relative URL of the farmer's own photo of this crop, e.g. "/uploads/farm-crops/x.jpg".</summary>
    public string? ImageUrl { get; private set; }

    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>Plot outline for the map, as an ordered ring of points. Empty when not drawn.</summary>
    public IReadOnlyList<GeoPoint> Boundary { get; private set; } = [];

    public string? Notes { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private FarmCrop(Guid farmId, string cropId, DateOnly? plantingDate, double? plotSizeHectares)
    {
        FarmId = farmId;
        CropId = cropId;
        PlantingDate = plantingDate;
        PlotSizeHectares = plotSizeHectares;
        Status = FarmCropStatus.Active;
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

    /// <summary>Replaces everything the farmer can edit on a crop card. Values are expected to be validated already.</summary>
    public void UpdateDetails(
        string cropId,
        string? plotName,
        string? variety,
        DateOnly? plantingDate,
        DateOnly? expectedHarvestDate,
        double? plotSizeHectares,
        double? latitude,
        double? longitude,
        IReadOnlyList<GeoPoint>? boundary,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new ArgumentException("Crop id is required.", nameof(cropId));
        }

        if (plotSizeHectares is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plotSizeHectares), "Plot size must be positive.");
        }

        if (latitude.HasValue != longitude.HasValue || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ArgumentException("Latitude and longitude must both be given and in range.", nameof(latitude));
        }

        if (plantingDate.HasValue && expectedHarvestDate.HasValue && expectedHarvestDate <= plantingDate)
        {
            throw new ArgumentException("Expected harvest must be after planting.", nameof(expectedHarvestDate));
        }

        CropId = cropId.Trim().ToLowerInvariant();
        PlotName = Clean(plotName);
        Variety = Clean(variety);
        PlantingDate = plantingDate;
        ExpectedHarvestDate = expectedHarvestDate;
        PlotSizeHectares = plotSizeHectares;
        Latitude = latitude;
        Longitude = longitude;
        Boundary = boundary?.ToList() ?? [];
        Notes = Clean(notes);
        Touch();
    }

    public void MarkHarvested(DateOnly harvestedOn)
    {
        Status = FarmCropStatus.Harvested;
        HarvestedOn = harvestedOn;
        Touch();
    }

    public void MarkFailed()
    {
        Status = FarmCropStatus.Failed;
        HarvestedOn = null;
        Touch();
    }

    /// <summary>Back to the field, e.g. after marking harvested by mistake.</summary>
    public void Reactivate()
    {
        Status = FarmCropStatus.Active;
        HarvestedOn = null;
        Touch();
    }

    public void SetImage(string? imageUrl)
    {
        ImageUrl = Clean(imageUrl);
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
