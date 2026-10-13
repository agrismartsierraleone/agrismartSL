using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;

namespace AgrismartAPI.Application.MyFarm;

/// <summary>What My Farm knows about one crop key, merged from the harvest and fertilizer catalogues.</summary>
/// <param name="TypicalDaysToMaturity">Planting to harvest, in days. Null for perennials with no catalogue value.</param>
/// <param name="Stages">Growth stages by fraction of the season, for the "Growth Stage" label.</param>
/// <param name="FertilizerSchedule">Split applications, for the fertilizer tasks. Empty when unknown.</param>
public record CropInfo(
    string Id,
    string Name,
    string? LocalName,
    int? TypicalDaysToMaturity,
    bool IsPerennial,
    IReadOnlyList<GrowthStage> Stages,
    IReadOnlyList<FertilizerSplit> FertilizerSchedule);

/// <summary>The crop keys a farm can use: union of GET /crops and GET /fertilizer/crops.</summary>
public class CropCatalog
{
    // Crops only in the fertilizer catalogue have no days-to-maturity there.
    private static readonly Dictionary<string, int> FallbackDaysToMaturity = new() { ["okra"] = 60 };

    // Display stages for rice match how Sierra Leonean farmers and extension officers describe the crop
    // ("Tillering" rather than the generic cereal "Vegetative").
    private static readonly IReadOnlyList<GrowthStage> RiceStages =
    [
        new("Seedling", 0.00), new("Tillering", 0.15), new("Panicle initiation", 0.50),
        new("Flowering", 0.65), new("Grain filling", 0.75), new("Maturity", 0.92)
    ];

    private readonly Dictionary<string, CropInfo> crops;

    public CropCatalog(IEnumerable<CropProfile> harvestCrops, IEnumerable<FertilizerCropProfile> fertilizerCrops)
    {
        var harvest = harvestCrops.ToDictionary(crop => crop.Key);
        var fertilizer = fertilizerCrops.ToDictionary(crop => crop.Key);

        crops = harvest.Keys.Union(fertilizer.Keys).ToDictionary(key => key, key =>
        {
            harvest.TryGetValue(key, out var h);
            fertilizer.TryGetValue(key, out var f);
            var isPerennial = f?.IsPerennial ?? false;
            int? days = h?.TypicalDaysToMaturity ?? (FallbackDaysToMaturity.TryGetValue(key, out var fallback) ? fallback : null);
            var stages = key.StartsWith("rice", StringComparison.Ordinal) ? RiceStages : h?.Stages ?? [];
            return new CropInfo(key, h?.Name ?? f!.Name, f?.LocalName, isPerennial ? null : days, isPerennial, stages,
                f?.Schedule ?? []);
        });
    }

    public IReadOnlyCollection<CropInfo> All => crops.Values;

    public bool Contains(string cropId) => crops.ContainsKey(cropId);

    /// <summary>Never null: unknown keys (e.g. a crop later removed from the catalogue) get a bare entry.</summary>
    public CropInfo Get(string? cropId) =>
        cropId is not null && crops.TryGetValue(cropId, out var crop)
            ? crop
            : new CropInfo(cropId ?? "", Humanize(cropId), null, null, false, [], []);

    private static string Humanize(string? key) =>
        string.IsNullOrEmpty(key) ? "Crop" : char.ToUpperInvariant(key[0]) + key[1..].Replace('-', ' ');
}
