using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Harvest;

/// <param name="Name">Stage name, e.g. "Flowering".</param>
/// <param name="StartFraction">Fraction of GddToMaturity at which this stage begins (0.0 - 1.0).</param>
public record GrowthStage(string Name, double StartFraction);

/// <summary>
/// Thermal-time profile for one crop. These numbers drive the whole prediction, so they
/// are the first thing to calibrate against local trial data before this goes near a real farmer.
/// </summary>
public class CropProfile : Entity
{
    /// <summary>Stable lowercase key used in API requests, e.g. "maize".</summary>
    public string Key { get; private set; }

    public string Name { get; private set; }

    /// <summary>Lower developmental threshold (T-base). Below this the crop accumulates no thermal time.</summary>
    public double BaseTemperatureC { get; private set; }

    /// <summary>Upper cut-off. Temperatures above this contribute no extra development (Method B / modified GDD).</summary>
    public double UpperTemperatureC { get; private set; }

    /// <summary>Accumulated GDD from planting to harvest maturity.</summary>
    public double GddToMaturity { get; private set; }

    /// <summary>Calendar-day rule of thumb, used only as a sanity check.</summary>
    public int TypicalDaysToMaturity { get; private set; }

    /// <summary>Phenological stages, keyed by the fraction of total GDD at which each begins.</summary>
    public IReadOnlyList<GrowthStage> Stages { get; private set; }

    private CropProfile(
        string key,
        string name,
        double baseTemperatureC,
        double upperTemperatureC,
        double gddToMaturity,
        int typicalDaysToMaturity,
        IReadOnlyList<GrowthStage> stages)
    {
        Key = key;
        Name = name;
        BaseTemperatureC = baseTemperatureC;
        UpperTemperatureC = upperTemperatureC;
        GddToMaturity = gddToMaturity;
        TypicalDaysToMaturity = typicalDaysToMaturity;
        Stages = stages;
    }

    // EF Core
    private CropProfile()
    {
        Key = null!;
        Name = null!;
        Stages = new List<GrowthStage>();
    }

    public static CropProfile Create(
        string key,
        string name,
        double baseTemperatureC,
        double upperTemperatureC,
        double gddToMaturity,
        int typicalDaysToMaturity,
        IReadOnlyList<GrowthStage> stages)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (gddToMaturity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gddToMaturity), "GDD to maturity must be positive.");
        }

        return new CropProfile(
            key.Trim().ToLowerInvariant(),
            name.Trim(),
            baseTemperatureC,
            upperTemperatureC,
            gddToMaturity,
            typicalDaysToMaturity,
            stages);
    }
}
