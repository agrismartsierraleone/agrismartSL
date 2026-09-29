using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Fertilizer;

/// <summary>One application in a crop's split-fertilizer schedule.</summary>
/// <param name="Stage">Display name, e.g. "Basal (at planting)".</param>
/// <param name="DaysAfterPlanting">
/// Days after planting/transplanting. For perennials (cocoa, oil palm) this is days after the start of
/// the rainy season — see <paramref name="Timing"/>.
/// </param>
/// <param name="Timing">Plain-language timing a farmer can follow, e.g. "4-6 weeks after planting".</param>
/// <param name="NitrogenPercent">Share of the season's total N applied at this stage (0-100).</param>
/// <param name="PhosphatePercent">Share of the season's total P2O5 applied at this stage (0-100).</param>
/// <param name="PotashPercent">Share of the season's total K2O applied at this stage (0-100).</param>
public record FertilizerSplit(
    string Stage,
    int DaysAfterPlanting,
    string Timing,
    double NitrogenPercent,
    double PhosphatePercent,
    double PotashPercent);

/// <summary>
/// Nutrient demand and application schedule for one crop. Kept separate from the harvest
/// <c>CropProfile</c> (thermal-time data) so neither feature's reference data constrains the other;
/// <see cref="Key"/> matches the harvest crop key wherever both exist.
/// </summary>
public class FertilizerCropProfile : Entity
{
    /// <summary>Tolerance when checking that each nutrient's schedule adds up to 100%.</summary>
    private const double SplitSumTolerance = 0.5;

    /// <summary>Stable lowercase key used in API requests, e.g. "maize".</summary>
    public string Key { get; private set; }

    public string Name { get; private set; }

    /// <summary>Krio / local name, if known.</summary>
    public string? LocalName { get; private set; }

    /// <summary>N required (kg/ha) to produce <see cref="ReferenceYieldTPerHa"/>.</summary>
    public double NitrogenKgPerHa { get; private set; }

    /// <summary>P2O5 required (kg/ha) to produce <see cref="ReferenceYieldTPerHa"/>.</summary>
    public double PhosphateKgPerHa { get; private set; }

    /// <summary>K2O required (kg/ha) to produce <see cref="ReferenceYieldTPerHa"/>.</summary>
    public double PotashKgPerHa { get; private set; }

    /// <summary>Yield (t/ha) the base nutrient rates are calibrated for.</summary>
    public double ReferenceYieldTPerHa { get; private set; }

    /// <summary>Highest target yield (t/ha) the calculator will accept before capping.</summary>
    public double MaxRealisticYieldTPerHa { get; private set; }

    /// <summary>What the yield figures measure, e.g. "paddy", "fresh roots", "dry beans".</summary>
    public string YieldBasis { get; private set; }

    /// <summary>Typical plants (or stands) per hectare; enables grams-per-plant guidance. Null for broadcast crops.</summary>
    public int? PlantsPerHectare { get; private set; }

    /// <summary>True for tree crops whose rates are per hectare per year of a mature plantation.</summary>
    public bool IsPerennial { get; private set; }

    /// <summary>Crop-specific agronomic advice appended to every recommendation.</summary>
    public string? Notes { get; private set; }

    public IReadOnlyList<FertilizerSplit> Schedule { get; private set; }

    private FertilizerCropProfile(string key)
    {
        Key = key;
        Name = null!;
        YieldBasis = null!;
        Schedule = null!;
    }

    // EF Core
    private FertilizerCropProfile()
    {
        Key = null!;
        Name = null!;
        YieldBasis = null!;
        Schedule = new List<FertilizerSplit>();
    }

    public static FertilizerCropProfile Create(
        string key,
        string name,
        string? localName,
        double nitrogenKgPerHa,
        double phosphateKgPerHa,
        double potashKgPerHa,
        double referenceYieldTPerHa,
        double maxRealisticYieldTPerHa,
        string yieldBasis,
        int? plantsPerHectare,
        bool isPerennial,
        string? notes,
        IReadOnlyList<FertilizerSplit> schedule)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        var crop = new FertilizerCropProfile(key.Trim().ToLowerInvariant());
        crop.Update(name, localName, nitrogenKgPerHa, phosphateKgPerHa, potashKgPerHa, referenceYieldTPerHa,
            maxRealisticYieldTPerHa, yieldBasis, plantsPerHectare, isPerennial, notes, schedule);
        return crop;
    }

    public void Update(
        string name,
        string? localName,
        double nitrogenKgPerHa,
        double phosphateKgPerHa,
        double potashKgPerHa,
        double referenceYieldTPerHa,
        double maxRealisticYieldTPerHa,
        string yieldBasis,
        int? plantsPerHectare,
        bool isPerennial,
        string? notes,
        IReadOnlyList<FertilizerSplit> schedule)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (nitrogenKgPerHa < 0 || phosphateKgPerHa < 0 || potashKgPerHa < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nitrogenKgPerHa), "Nutrient requirements cannot be negative.");
        }

        if (referenceYieldTPerHa <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceYieldTPerHa), "Reference yield must be positive.");
        }

        if (maxRealisticYieldTPerHa < referenceYieldTPerHa)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRealisticYieldTPerHa), "Max realistic yield cannot be below the reference yield.");
        }

        if (plantsPerHectare is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plantsPerHectare), "Plants per hectare must be positive when given.");
        }

        ValidateSchedule(schedule);

        Name = name.Trim();
        LocalName = string.IsNullOrWhiteSpace(localName) ? null : localName.Trim();
        NitrogenKgPerHa = nitrogenKgPerHa;
        PhosphateKgPerHa = phosphateKgPerHa;
        PotashKgPerHa = potashKgPerHa;
        ReferenceYieldTPerHa = referenceYieldTPerHa;
        MaxRealisticYieldTPerHa = maxRealisticYieldTPerHa;
        YieldBasis = string.IsNullOrWhiteSpace(yieldBasis) ? "yield" : yieldBasis.Trim();
        PlantsPerHectare = plantsPerHectare;
        IsPerennial = isPerennial;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Schedule = schedule.OrderBy(split => split.DaysAfterPlanting).ToList();
    }

    private static void ValidateSchedule(IReadOnlyList<FertilizerSplit> schedule)
    {
        if (schedule is null || schedule.Count == 0)
        {
            throw new ArgumentException("At least one application split is required.", nameof(schedule));
        }

        if (schedule.Any(split => string.IsNullOrWhiteSpace(split.Stage)))
        {
            throw new ArgumentException("Every split needs a stage name.", nameof(schedule));
        }

        if (schedule.Any(split => split.DaysAfterPlanting < 0 ||
                                  split.NitrogenPercent < 0 || split.PhosphatePercent < 0 || split.PotashPercent < 0))
        {
            throw new ArgumentException("Split days and percentages cannot be negative.", nameof(schedule));
        }

        CheckSum(schedule.Sum(split => split.NitrogenPercent), "N");
        CheckSum(schedule.Sum(split => split.PhosphatePercent), "P2O5");
        CheckSum(schedule.Sum(split => split.PotashPercent), "K2O");
    }

    private static void CheckSum(double total, string nutrient)
    {
        if (Math.Abs(total - 100) > SplitSumTolerance)
        {
            throw new ArgumentException($"Schedule {nutrient} percentages must add up to 100 (got {total}).", "schedule");
        }
    }
}
