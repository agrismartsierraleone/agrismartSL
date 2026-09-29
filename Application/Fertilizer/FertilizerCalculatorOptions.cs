namespace AgrismartAPI.Application.Fertilizer;

/// <summary>
/// Tunable multipliers and thresholds for the fertilizer calculator, bound from the
/// "FertilizerCalculator" section of appsettings.json. The defaults here mirror appsettings.json so
/// the calculator behaves the same when the section is missing (e.g. in unit tests).
/// Crop and product values live in Infrastructure/Persistence/Seed/FertilizerReferenceData.cs.
/// </summary>
public class FertilizerCalculatorOptions
{
    public const string SectionName = "FertilizerCalculator";

    public string Currency { get; set; } = "SLE";

    /// <summary>Lower bound on TargetYield / ReferenceYield when scaling nutrient demand.</summary>
    public double MinYieldScale { get; set; } = 0.5;

    /// <summary>Upper bound on TargetYield / ReferenceYield — demand does not rise linearly forever.</summary>
    public double MaxYieldScale { get; set; } = 2.0;

    /// <summary>Simple mode: multiplier on all three nutrients per fertility class.</summary>
    public SoilClassMultipliers SoilFertilityMultipliers { get; set; } = new();

    /// <summary>Extra multiplier on N only, by soil texture (sandy soils leach N).</summary>
    public SoilTypeMultipliers SoilTypeNitrogenMultipliers { get; set; } = new();

    /// <summary>Soil-test mode: Low/Medium/High class boundaries per nutrient.</summary>
    public SoilTestThresholds SoilTest { get; set; } = new();

    /// <summary>Below this pH, recommend liming.</summary>
    public double LimingPhThreshold { get; set; } = 5.5;

    /// <summary>Above this pH, add the alkaline-soil note.</summary>
    public double AlkalinePhThreshold { get; set; } = 7.5;

    /// <summary>Warn when a nutrient is over-supplied by more than this % of the requirement.</summary>
    public double OverSupplyTolerancePercent { get; set; } = 15;

    /// <summary>Warn when mineral (non-organic) fertilizer exceeds this many kg per hectare in total.</summary>
    public double MaxMineralProductKgPerHa { get; set; } = 800;

    /// <summary>Bags are rounded UP to a multiple of this (0.5 = nearest half bag).</summary>
    public double BagRoundingIncrement { get; set; } = 0.5;

    /// <summary>Approximate weight of one farmer's handful of granular fertilizer, in grams.</summary>
    public double GramsPerHandful { get; set; } = 40;

    /// <summary>Approximate weight of one level soft-drink bottle cap of granular fertilizer, in grams.</summary>
    public double GramsPerBottleCap { get; set; } = 5;

    /// <summary>Largest area accepted by the calculator, in hectares.</summary>
    public double MaxAreaHectares { get; set; } = 10_000;

    /// <summary>
    /// Candidate product sets tried when the farmer selects nothing, by product key, in order of
    /// preference: the first set that meets the full requirement wins, unless every candidate is fully
    /// priced, in which case the cheapest wins. Leave empty to use
    /// <see cref="DefaultCandidatePlans"/>. (Kept empty by default because the configuration binder
    /// appends to, rather than replaces, pre-populated lists.)
    /// </summary>
    public List<List<string>> AutomaticCandidatePlans { get; set; } = [];

    public static readonly IReadOnlyList<IReadOnlyList<string>> DefaultCandidatePlans =
    [
        ["npk-15-15-15", "urea", "tsp", "mop"],
        ["npk-20-10-10", "urea", "tsp", "mop"],
        ["urea", "tsp", "mop"],
        ["dap", "urea", "mop"]
    ];

    public IReadOnlyList<IReadOnlyList<string>> EffectiveCandidatePlans =>
        AutomaticCandidatePlans.Count > 0 ? AutomaticCandidatePlans : DefaultCandidatePlans;
}

public class SoilClassMultipliers
{
    public double Low { get; set; } = 1.25;
    public double Medium { get; set; } = 1.0;
    public double High { get; set; } = 0.6;

    public double For(SoilFertility fertility) => fertility switch
    {
        SoilFertility.Low => Low,
        SoilFertility.High => High,
        _ => Medium
    };
}

public class SoilTypeMultipliers
{
    public double Sandy { get; set; } = 1.1;
    public double Loamy { get; set; } = 1.0;
    public double Clay { get; set; } = 1.0;

    public double For(SoilType? soilType) => soilType switch
    {
        SoilType.Sandy => Sandy,
        SoilType.Clay => Clay,
        _ => Loamy
    };
}

/// <summary>A value below <see cref="LowBelow"/> is Low, above <see cref="HighAbove"/> is High, otherwise Medium.</summary>
public class NutrientThreshold
{
    public double LowBelow { get; set; }
    public double HighAbove { get; set; }

    public SoilFertility Classify(double value) =>
        value < LowBelow ? SoilFertility.Low : value > HighAbove ? SoilFertility.High : SoilFertility.Medium;
}

public class SoilTestThresholds
{
    /// <summary>Total N, % (Kjeldahl).</summary>
    public NutrientThreshold NitrogenTotalPercent { get; set; } = new() { LowBelow = 0.10, HighAbove = 0.20 };

    /// <summary>Available P, mg/kg (Bray-1).</summary>
    public NutrientThreshold PhosphorusBray1MgPerKg { get; set; } = new() { LowBelow = 10, HighAbove = 20 };

    /// <summary>Exchangeable K, cmol(+)/kg (ammonium acetate).</summary>
    public NutrientThreshold PotassiumCmolPerKg { get; set; } = new() { LowBelow = 0.20, HighAbove = 0.40 };

    /// <summary>
    /// Multiplier applied to a nutrient's requirement for each soil-test class. This is the
    /// "soil supply credit": a High class means the soil already supplies ~40% of the demand.
    /// </summary>
    public SoilClassMultipliers ClassMultipliers { get; set; } = new();
}
