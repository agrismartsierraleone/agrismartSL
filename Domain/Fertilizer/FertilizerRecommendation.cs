using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Fertilizer;

/// <summary>
/// A saved fertilizer calculation. The full request and result are stored as JSON snapshots so the
/// history reads exactly as the farmer saw it, even after reference data or formulas change.
/// </summary>
public class FertilizerRecommendation : Entity
{
    public Guid UserId { get; private init; }

    /// <summary>Crop key at the time of calculation, e.g. "maize".</summary>
    public string CropId { get; private init; }

    /// <summary>Crop display name, snapshotted.</summary>
    public string CropName { get; private init; }

    /// <summary>Optional farmer-chosen label for the farm/plot, e.g. "Back field by the stream".</summary>
    public string? PlotLabel { get; private init; }

    public double AreaHectares { get; private init; }

    public decimal TotalEstimatedCost { get; private init; }

    public string Currency { get; private init; }

    public string InputsJson { get; private init; }

    public string ResultJson { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    private FertilizerRecommendation(
        Guid userId,
        string cropId,
        string cropName,
        string? plotLabel,
        double areaHectares,
        decimal totalEstimatedCost,
        string currency,
        string inputsJson,
        string resultJson)
    {
        UserId = userId;
        CropId = cropId;
        CropName = cropName;
        PlotLabel = plotLabel;
        AreaHectares = areaHectares;
        TotalEstimatedCost = totalEstimatedCost;
        Currency = currency;
        InputsJson = inputsJson;
        ResultJson = resultJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private FertilizerRecommendation()
    {
        CropId = null!;
        CropName = null!;
        Currency = null!;
        InputsJson = null!;
        ResultJson = null!;
    }

    public static FertilizerRecommendation Create(
        Guid userId,
        string cropId,
        string cropName,
        string? plotLabel,
        double areaHectares,
        decimal totalEstimatedCost,
        string currency,
        string inputsJson,
        string resultJson)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(cropId))
        {
            throw new ArgumentException("Crop id is required.", nameof(cropId));
        }

        return new FertilizerRecommendation(
            userId, cropId, cropName, string.IsNullOrWhiteSpace(plotLabel) ? null : plotLabel.Trim(),
            areaHectares, totalEstimatedCost, currency, inputsJson, resultJson);
    }
}
