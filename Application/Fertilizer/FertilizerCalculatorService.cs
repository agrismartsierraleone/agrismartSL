using System.Globalization;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Fertilizer;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Fertilizer;

/// <summary>
/// Turns crop demand, soil condition and available products into a practical fertilizer plan.
/// See README.fertilizer.md for the formulas; every tunable number comes from
/// <see cref="FertilizerCalculatorOptions"/> or the crop/product reference data.
/// </summary>
public class FertilizerCalculatorService(IOptions<FertilizerCalculatorOptions> options) : IFertilizerCalculatorService
{
    /// <summary>The "bags50Kg" field always counts standard 50 kg bags, whatever the product's own bag size.</summary>
    public const double StandardBagKg = 50;

    /// <summary>Nutrient gaps below this many kg are treated as met (float noise, not agronomy).</summary>
    private const double GapToleranceKg = 0.05;

    /// <summary>Straight fertilizers fill P first, then K, then N — so N carried by DAP is counted before Urea is sized.</summary>
    private static readonly Nutrient[] FillOrder = [Nutrient.P, Nutrient.K, Nutrient.N];

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private sealed record PlanLine(FertilizerProduct Product, double QuantityKg);

    private sealed record Plan(IReadOnlyList<PlanLine> Lines, Nutrients Supplied, Nutrients Deficit, Nutrients Excess);

    public FertilizerCalculationResponseDto Calculate(
        FertilizerCalculationRequest request,
        FertilizerCropProfile crop,
        IReadOnlyList<FertilizerProduct> catalogue)
    {
        var settings = options.Value;
        var errors = FertilizerRequestValidator.Validate(request, catalogue, settings.MaxAreaHectares);
        if (errors.Count > 0)
        {
            throw FertilizerRequestValidator.ToException(errors);
        }

        var soil = request.Soil!;
        var warnings = new List<string>();
        var notes = new List<string>();
        var prices = ResolvePrices(request, catalogue);

        // 0. Area in hectares.
        var areaHa = AreaConverter.ToHectares(request.Area, request.AreaUnit, request.LengthM, request.WidthM);

        // 1. Yield scaling: requirement ∝ target yield, capped at the crop's realistic maximum.
        var targetYield = request.TargetYieldTPerHa ?? crop.ReferenceYieldTPerHa;
        var yieldCapped = false;
        if (targetYield > crop.MaxRealisticYieldTPerHa)
        {
            warnings.Add($"Target yield {F1(targetYield)} t/ha is above the realistic maximum for {crop.Name} " +
                         $"({F1(crop.MaxRealisticYieldTPerHa)} t/ha); the calculation uses {F1(crop.MaxRealisticYieldTPerHa)} t/ha.");
            targetYield = crop.MaxRealisticYieldTPerHa;
            yieldCapped = true;
        }

        var rawScale = targetYield / crop.ReferenceYieldTPerHa;
        var yieldScale = Math.Clamp(rawScale, settings.MinYieldScale, settings.MaxYieldScale);
        if (Math.Abs(yieldScale - rawScale) > 1e-9)
        {
            notes.Add($"Nutrient demand was scaled by {F2(yieldScale)}× rather than {F2(rawScale)}× — the calculator limits " +
                      $"yield scaling to {F2(settings.MinYieldScale)}–{F2(settings.MaxYieldScale)}× the reference yield.");
        }

        var baseRequirementPerHa = new Nutrients(crop.NitrogenKgPerHa, crop.PhosphateKgPerHa, crop.PotashKgPerHa) * yieldScale;

        // 2. Soil adjustment (never below zero).
        var soilAdjustment = ResolveSoilAdjustment(soil, settings, notes);
        var requirementPerHa = new Nutrients(
            baseRequirementPerHa.N * soilAdjustment.NitrogenFactor,
            baseRequirementPerHa.P * soilAdjustment.PhosphateFactor,
            baseRequirementPerHa.K * soilAdjustment.PotashFactor).ClampAtZero();

        // 3. Whole-area requirement.
        var required = requirementPerHa * areaHa;

        // 4. Products: the farmer's selection, or the best automatic plan.
        var selectedKeys = (request.FertilizerProductIds ?? [])
            .Select(id => id.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        var selectionMode = selectedKeys.Count > 0 ? FertilizerSelectionMode.FarmerSelected : FertilizerSelectionMode.Automatic;

        Plan plan;
        if (selectionMode == FertilizerSelectionMode.FarmerSelected)
        {
            var selected = selectedKeys.Select(key => catalogue.First(p => p.Key == key)).ToList();
            plan = BuildPlan(required, selected);
            AddDeficitWarnings(plan, required, selected, warnings);

            var unused = selected.Where(p => plan.Lines.All(line => line.Product.Key != p.Key)).ToList();
            foreach (var product in unused)
            {
                notes.Add(product.NitrogenPercent > 0 && product.PhosphatePercent > 0 && product.PotashPercent > 0
                    ? $"{product.Name} was not used: the soil and crop need little or none of one of its nutrients, so any amount would over-apply it. Straight fertilizers fit this field better."
                    : $"{product.Name} was not needed — the other selected products already cover what it supplies.");
            }
        }
        else
        {
            plan = ChooseAutomaticPlan(required, catalogue, prices, settings, out var automaticNote);
            notes.Add(automaticNote);
            AddDeficitWarnings(plan, required, [], warnings);
        }

        AddExcessWarnings(plan, required, settings, warnings);

        // 5. Practical units + 7. cost.
        var productDtos = new List<ProductQuantityDto>();
        decimal totalCost = 0;
        var costComplete = true;
        var unpriced = new List<string>();
        foreach (var line in plan.Lines)
        {
            var bags = RoundUpTo(line.QuantityKg / line.Product.BagSizeKg, settings.BagRoundingIncrement);
            var bags50 = RoundUpTo(line.QuantityKg / StandardBagKg, settings.BagRoundingIncrement);
            var price = prices.GetValueOrDefault(line.Product.Key);
            var cost = price is { } p ? Math.Round((decimal)bags * p, 2, MidpointRounding.AwayFromZero) : 0m;
            if (price is null)
            {
                costComplete = false;
                unpriced.Add(line.Product.Name);
            }

            totalCost += cost;
            productDtos.Add(new ProductQuantityDto(
                line.Product.Key,
                line.Product.Name,
                line.Product.Type,
                Nutrients.Round1(line.QuantityKg),
                line.Product.BagSizeKg,
                bags,
                bags50,
                price ?? 0m,
                price is not null,
                cost,
                Nutrients.SuppliedBy(line.Product, line.QuantityKg).ToDto()));

            if (line.QuantityKg < line.Product.BagSizeKg * 0.5)
            {
                notes.Add($"Only {F1(line.QuantityKg)} kg of {line.Product.Name} is needed — less than half a bag. " +
                          "Buy it loose by the kilogram if your supplier sells it that way, or share a bag with a neighbour.");
            }
        }

        if (plan.Lines.Count > 0 && unpriced.Count > 0)
        {
            notes.Add(unpriced.Count == plan.Lines.Count
                ? "No prices are available for these products, so no cost estimate is given. Enter local prices to see costs."
                : $"The total cost excludes {string.Join(", ", unpriced)} (no price available).");
        }

        var mineralKgPerHa = plan.Lines.Where(l => l.Product.Type != FertilizerProductType.Organic).Sum(l => l.QuantityKg) / areaHa;
        if (mineralKgPerHa > settings.MaxMineralProductKgPerHa)
        {
            warnings.Add($"This plan uses about {F0(mineralKgPerHa)} kg/ha of mineral fertilizer, above the " +
                         $"{F0(settings.MaxMineralProductKgPerHa)} kg/ha safety guide. Very high rates can scorch plants and waste money — " +
                         "consider a lower target yield or confirm with an extension officer.");
        }

        // 6. Split into the crop's application schedule.
        var plantCount = crop.PlantsPerHectare is { } plantsPerHa ? (int)Math.Round(plantsPerHa * areaHa) : 0;
        var schedule = BuildSchedule(plan, crop, plantCount, settings);

        // 8. Agronomic notes.
        AddAgronomicNotes(soil, soilAdjustment, plan, crop, settings, notes);

        if (plan.Lines.Count == 0 && required.Total <= GapToleranceKg)
        {
            notes.Insert(0, "No fertilizer is needed for this crop, target yield and soil condition.");
        }

        return new FertilizerCalculationResponseDto(
            crop.Key,
            crop.Name,
            Math.Round(areaHa, 4, MidpointRounding.AwayFromZero),
            Nutrients.Round1(targetYield),
            yieldCapped,
            crop.YieldBasis,
            soilAdjustment,
            requirementPerHa.ToDto(),
            required.ToDto(),
            selectionMode,
            productDtos,
            plan.Supplied.ToDto(),
            (plan.Supplied - required).ToDto(),
            schedule,
            plantCount,
            totalCost,
            costComplete,
            settings.Currency,
            warnings,
            notes);
    }

    // ---------- Soil ----------

    private static SoilAdjustmentDto ResolveSoilAdjustment(SoilInputDto soil, FertilizerCalculatorOptions settings, List<string> notes)
    {
        var mode = FertilizerRequestValidator.ResolveMode(soil);
        SoilFertility nClass, pClass, kClass;
        double nFactor, pFactor, kFactor;

        if (mode == SoilInputMode.Simple)
        {
            nClass = pClass = kClass = soil.Fertility!.Value;
            nFactor = pFactor = kFactor = settings.SoilFertilityMultipliers.For(nClass);
        }
        else
        {
            var thresholds = settings.SoilTest;
            nClass = thresholds.NitrogenTotalPercent.Classify(soil.NitrogenTotalPercent!.Value);
            pClass = thresholds.PhosphorusBray1MgPerKg.Classify(soil.PhosphorusBray1MgPerKg!.Value);
            kClass = thresholds.PotassiumCmolPerKg.Classify(soil.PotassiumCmolPerKg!.Value);
            nFactor = thresholds.ClassMultipliers.For(nClass);
            pFactor = thresholds.ClassMultipliers.For(pClass);
            kFactor = thresholds.ClassMultipliers.For(kClass);

            notes.Add($"Soil test: nitrogen {Lower(nClass)} ({F2(soil.NitrogenTotalPercent.Value)}% total N), " +
                      $"phosphorus {Lower(pClass)} ({F1(soil.PhosphorusBray1MgPerKg.Value)} mg/kg Bray-1 P), " +
                      $"potassium {Lower(kClass)} ({F2(soil.PotassiumCmolPerKg.Value)} cmol/kg exchangeable K).");
        }

        nFactor *= settings.SoilTypeNitrogenMultipliers.For(soil.SoilType);

        return new SoilAdjustmentDto(
            mode, nClass, pClass, kClass,
            Math.Round(Math.Max(0, nFactor), 3), Math.Round(Math.Max(0, pFactor), 3), Math.Round(Math.Max(0, kFactor), 3));
    }

    // ---------- Product planning ----------

    /// <summary>A "complete" product carries all three of N, P2O5 and K2O (NPK blends, manure) and is used as the base.</summary>
    private static bool IsComplete(FertilizerProduct product) =>
        product.NitrogenPercent > 0 && product.PhosphatePercent > 0 && product.PotashPercent > 0;

    /// <summary>
    /// Limiting-nutrient rule: the most compound that can be applied without exceeding the remaining
    /// requirement of ANY nutrient it contains.
    /// </summary>
    private static double LimitingQuantityKg(FertilizerProduct product, Nutrients remaining)
    {
        double? limit = null;
        foreach (var nutrient in FillOrder)
        {
            var content = Nutrients.Content(product, nutrient);
            if (content <= 0)
            {
                continue;
            }

            var kg = Math.Max(0, remaining[nutrient]) / (content / 100);
            limit = limit is null ? kg : Math.Min(limit.Value, kg);
        }

        return limit ?? 0;
    }

    private static double Overshoot(Nutrients supplied, Nutrients remaining) =>
        Math.Max(0, supplied.N - Math.Max(0, remaining.N)) +
        Math.Max(0, supplied.P - Math.Max(0, remaining.P)) +
        Math.Max(0, supplied.K - Math.Max(0, remaining.K));

    private static Plan BuildPlan(Nutrients required, IReadOnlyList<FertilizerProduct> products)
    {
        var quantities = new List<(FertilizerProduct Product, double Kg)>();
        var remaining = required;

        void Apply(FertilizerProduct product, double kg)
        {
            var index = quantities.FindIndex(q => q.Product.Key == product.Key);
            if (index >= 0)
            {
                quantities[index] = (product, quantities[index].Kg + kg);
            }
            else
            {
                quantities.Add((product, kg));
            }

            remaining -= Nutrients.SuppliedBy(product, kg);
        }

        // Base: the complete product that delivers the most nutrient at its limiting rate.
        var complete = products.Where(IsComplete).ToList();
        if (complete.Count > 0)
        {
            var best = complete
                .Select(product => (Product: product, Kg: LimitingQuantityKg(product, remaining)))
                .OrderByDescending(candidate => Nutrients.SuppliedBy(candidate.Product, candidate.Kg).Total)
                .ThenBy(candidate => candidate.Product.Key, StringComparer.Ordinal)
                .First();
            if (best.Kg > GapToleranceKg)
            {
                Apply(best.Product, best.Kg);
            }
        }

        // Fill: straight / two-nutrient products close the remaining gaps, P then K then N.
        var fillers = products.Where(product => !IsComplete(product)).ToList();
        foreach (var nutrient in FillOrder)
        {
            var gap = remaining[nutrient];
            if (gap <= GapToleranceKg)
            {
                continue;
            }

            var pick = fillers
                .Where(product => Nutrients.Content(product, nutrient) > 0)
                .Select(product =>
                {
                    var kg = gap / (Nutrients.Content(product, nutrient) / 100);
                    return (Product: product, Kg: kg, Overshoot: Overshoot(Nutrients.SuppliedBy(product, kg), remaining));
                })
                .OrderBy(candidate => Math.Round(candidate.Overshoot, 1))
                .ThenByDescending(candidate => Nutrients.Content(candidate.Product, nutrient))
                .ThenBy(candidate => candidate.Product.Key, StringComparer.Ordinal)
                .FirstOrDefault();

            if (pick.Product is not null)
            {
                Apply(pick.Product, pick.Kg);
            }
        }

        var lines = quantities.Select(q => new PlanLine(q.Product, q.Kg)).ToList();
        var supplied = lines.Aggregate(Nutrients.Zero, (sum, line) => sum + Nutrients.SuppliedBy(line.Product, line.QuantityKg));
        return new Plan(lines, supplied, (required - supplied).ClampAtZero(), (supplied - required).ClampAtZero());
    }

    /// <summary>
    /// Tries each configured candidate product set and keeps: smallest deficit, then lowest cost (only
    /// when every candidate is fully priced), then the first in configured order. The first configured
    /// plan (NPK 15-15-15 + Urea, topped up with TSP/MOP only when needed) is therefore the default.
    /// </summary>
    private Plan ChooseAutomaticPlan(
        Nutrients required,
        IReadOnlyList<FertilizerProduct> catalogue,
        IReadOnlyDictionary<string, decimal?> prices,
        FertilizerCalculatorOptions settings,
        out string note)
    {
        var byKey = catalogue.ToDictionary(product => product.Key);
        var candidates = settings.EffectiveCandidatePlans
            .Select(keys => keys.Select(key => byKey.GetValueOrDefault(key.Trim().ToLowerInvariant()))
                .OfType<FertilizerProduct>()
                .ToList())
            .Where(set => set.Count > 0)
            .Select(set => BuildPlan(required, set))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates.Add(BuildPlan(required, catalogue.Where(p => p.Type != FertilizerProductType.Organic).ToList()));
        }

        decimal? PlanCost(Plan plan)
        {
            decimal total = 0;
            foreach (var line in plan.Lines)
            {
                if (prices.GetValueOrDefault(line.Product.Key) is not { } price)
                {
                    return null;
                }

                total += (decimal)RoundUpTo(line.QuantityKg / line.Product.BagSizeKg, settings.BagRoundingIncrement) * price;
            }

            return total;
        }

        // OrderBy is stable, so ties keep the configured order — that order is the preference.
        var allPriced = candidates.All(plan => PlanCost(plan) is not null);
        var chosen = candidates
            .OrderBy(plan => Math.Round(plan.Deficit.Total, 1))
            .ThenBy(plan => allPriced ? PlanCost(plan)!.Value : 0m)
            .First();

        var names = chosen.Lines.Count == 0 ? "no products" : string.Join(" + ", chosen.Lines.Select(line => line.Product.Name));
        note = allPriced
            ? $"No fertilizers were selected, so the lowest-cost combination was chosen: {names}."
            : $"No fertilizers were selected, so the standard recommendation was used: {names}.";
        return chosen;
    }

    private static Dictionary<string, decimal?> ResolvePrices(FertilizerCalculationRequest request, IReadOnlyList<FertilizerProduct> catalogue)
    {
        var prices = catalogue.ToDictionary(product => product.Key, product => product.PricePerBag);
        foreach (var custom in request.CustomPrices ?? [])
        {
            prices[custom.ProductId.Trim().ToLowerInvariant()] = custom.PricePerBag;
        }

        return prices;
    }

    // ---------- Schedule ----------

    /// <summary>
    /// Products carrying P follow the P split; K-only products follow the K split; N-only products
    /// (Urea, Ammonium Sulphate) fill whatever each split's N share still lacks after the others.
    /// </summary>
    private static IReadOnlyList<ScheduleStageDto> BuildSchedule(
        Plan plan, FertilizerCropProfile crop, int plantCount, FertilizerCalculatorOptions settings)
    {
        var splits = crop.Schedule;
        var allocation = new double[plan.Lines.Count, splits.Count];

        static double[] Weights(IEnumerable<double> raw)
        {
            var values = raw.ToArray();
            var sum = values.Sum();
            return sum > 0 ? values.Select(v => v / sum).ToArray() : values.Select((_, i) => i == 0 ? 1.0 : 0.0).ToArray();
        }

        var pWeights = Weights(splits.Select(s => s.PhosphatePercent));
        var kWeights = Weights(splits.Select(s => s.PotashPercent));
        var nWeights = Weights(splits.Select(s => s.NitrogenPercent));

        for (var li = 0; li < plan.Lines.Count; li++)
        {
            var product = plan.Lines[li].Product;
            var weights = product.PhosphatePercent > 0 ? pWeights : product.PotashPercent > 0 ? kWeights : null;
            if (weights is null)
            {
                continue;
            }

            for (var si = 0; si < splits.Count; si++)
            {
                allocation[li, si] = plan.Lines[li].QuantityKg * weights[si];
            }
        }

        var nitrogenOnly = Enumerable.Range(0, plan.Lines.Count)
            .Where(li => plan.Lines[li].Product is { PhosphatePercent: <= 0, PotashPercent: <= 0 })
            .ToList();
        if (nitrogenOnly.Count > 0)
        {
            var shortfall = new double[splits.Count];
            for (var si = 0; si < splits.Count; si++)
            {
                var nAlready = 0.0;
                for (var li = 0; li < plan.Lines.Count; li++)
                {
                    nAlready += allocation[li, si] * plan.Lines[li].Product.NitrogenPercent / 100;
                }

                shortfall[si] = Math.Max(0, plan.Supplied.N * nWeights[si] - nAlready);
            }

            var nFill = shortfall.Sum() > GapToleranceKg ? Weights(shortfall) : nWeights;
            foreach (var li in nitrogenOnly)
            {
                for (var si = 0; si < splits.Count; si++)
                {
                    allocation[li, si] = plan.Lines[li].QuantityKg * nFill[si];
                }
            }
        }

        var unit = crop.IsPerennial ? "tree" : "plant";
        var stages = new List<ScheduleStageDto>();
        for (var si = 0; si < splits.Count; si++)
        {
            var products = new List<ScheduleProductDto>();
            var nutrients = Nutrients.Zero;
            for (var li = 0; li < plan.Lines.Count; li++)
            {
                var kg = allocation[li, si];
                if (kg <= GapToleranceKg)
                {
                    continue;
                }

                var product = plan.Lines[li].Product;
                nutrients += Nutrients.SuppliedBy(product, kg);
                var grams = plantCount > 0 ? kg * 1000 / plantCount : 0;
                products.Add(new ScheduleProductDto(
                    product.Key,
                    product.Name,
                    Nutrients.Round1(kg),
                    plantCount > 0 ? Math.Round(grams, 1, MidpointRounding.AwayFromZero) : 0,
                    plantCount > 0 ? PerPlantHint(grams, unit, settings) : ""));
            }

            if (products.Count == 0)
            {
                continue;
            }

            var split = splits[si];
            stages.Add(new ScheduleStageDto(split.Stage, split.DaysAfterPlanting, split.Timing, nutrients.ToDto(), products));
        }

        return stages;
    }

    private static string PerPlantHint(double grams, string unit, FertilizerCalculatorOptions settings)
    {
        if (grams < 1)
        {
            return $"less than 1 g per {unit} — mix with dry sand and spread thinly along the row";
        }

        if (grams < settings.GramsPerHandful / 2)
        {
            var caps = Math.Max(0.5, RoundToHalf(grams / settings.GramsPerBottleCap));
            return $"about {F1(caps)} level bottle cap{(caps > 1 ? "s" : "")} per {unit} (1 cap ≈ {F0(settings.GramsPerBottleCap)} g)";
        }

        var handfuls = Math.Max(0.5, RoundToHalf(grams / settings.GramsPerHandful));
        return $"about {F1(handfuls)} handful{(handfuls > 1 ? "s" : "")} per {unit} (1 handful ≈ {F0(settings.GramsPerHandful)} g)";
    }

    // ---------- Warnings and notes ----------

    private static string NutrientLabel(Nutrient nutrient) => nutrient switch
    {
        Nutrient.N => "nitrogen (N)",
        Nutrient.P => "phosphorus (P2O5)",
        _ => "potassium (K2O)"
    };

    private static string NutrientSymbol(Nutrient nutrient) => nutrient switch
    {
        Nutrient.N => "N",
        Nutrient.P => "P2O5",
        _ => "K2O"
    };

    private static string SuggestedSource(Nutrient nutrient) => nutrient switch
    {
        Nutrient.N => "Urea or Ammonium Sulphate",
        Nutrient.P => "TSP, SSP or DAP",
        _ => "MOP (Muriate of Potash)"
    };

    private static void AddDeficitWarnings(Plan plan, Nutrients required, IReadOnlyList<FertilizerProduct> selected, List<string> warnings)
    {
        foreach (var nutrient in new[] { Nutrient.N, Nutrient.P, Nutrient.K })
        {
            var deficit = plan.Deficit[nutrient];
            if (deficit <= Math.Max(0.1, required[nutrient] * 0.01))
            {
                continue;
            }

            var anySource = selected.Any(product => Nutrients.Content(product, nutrient) > 0) ||
                            plan.Lines.Any(line => Nutrients.Content(line.Product, nutrient) > 0);
            warnings.Add(anySource
                ? $"Your selected fertilizers cannot fully supply {NutrientLabel(nutrient)} — {NutrientSymbol(nutrient)} deficit of {F1(deficit)} kg. Add {SuggestedSource(nutrient)}."
                : $"No {NutrientLabel(nutrient).Split(' ')[0]} source selected — {NutrientSymbol(nutrient)} deficit of {F1(deficit)} kg. Add {SuggestedSource(nutrient)}.");
        }
    }

    private static void AddExcessWarnings(Plan plan, Nutrients required, FertilizerCalculatorOptions settings, List<string> warnings)
    {
        foreach (var nutrient in new[] { Nutrient.N, Nutrient.P, Nutrient.K })
        {
            var excess = plan.Excess[nutrient];
            var need = required[nutrient];
            if (excess <= Math.Max(0.5, need * settings.OverSupplyTolerancePercent / 100))
            {
                continue;
            }

            var sources = plan.Lines
                .Where(line => Nutrients.Content(line.Product, nutrient) > 0)
                .Select(line => line.Product.Name);
            var percent = need > 0 ? $" (+{F0(excess / need * 100)}%)" : "";
            warnings.Add($"{NutrientSymbol(nutrient)} is over-supplied by {F1(excess)} kg{percent} because it comes with {string.Join(", ", sources)}. " +
                         "Over-application wastes money and can harm the crop and nearby water — reduce or swap that product if you can.");
        }
    }

    private static void AddAgronomicNotes(
        SoilInputDto soil,
        SoilAdjustmentDto soilAdjustment,
        Plan plan,
        FertilizerCropProfile crop,
        FertilizerCalculatorOptions settings,
        List<string> notes)
    {
        if (soil.Ph is { } ph)
        {
            if (ph < settings.LimingPhThreshold)
            {
                notes.Add($"Soil pH {F1(ph)} is acidic (below {F1(settings.LimingPhThreshold)}). Apply agricultural lime or dolomite — " +
                          "typically 1–2 t/ha, spread and mixed into the soil 2–4 weeks before planting, ideally based on a lime-requirement test. " +
                          "Liming makes phosphorus and other fertilizer nutrients more available to the crop.");
            }
            else if (ph > settings.AlkalinePhThreshold)
            {
                notes.Add($"Soil pH {F1(ph)} is alkaline. Do not lime. Ammonium Sulphate is a good N source here, and watch for zinc deficiency.");
            }
        }
        else
        {
            notes.Add("Soil pH was not provided. Many upland soils in Sierra Leone are acidic — a simple pH test will show whether liming is needed.");
        }

        switch (soil.SoilType)
        {
            case SoilType.Sandy:
                notes.Add("Sandy soils lose nitrogen quickly: never apply more than the scheduled split at once, and top-dress when the soil is moist.");
                break;
            case SoilType.Clay:
                notes.Add("On clay soils, do not apply fertilizer while the soil is waterlogged; wait until it drains.");
                break;
        }

        if (soilAdjustment.Mode == SoilInputMode.Simple && soilAdjustment.NitrogenClass == SoilFertility.High)
        {
            notes.Add("High fertility reduces the recommended rates. If the crop shows yellowing or poor growth, have the soil tested.");
        }

        var used = plan.Lines.Select(line => line.Product).ToList();
        if (used.Any(p => p is { PhosphatePercent: <= 0, PotashPercent: <= 0 }))
        {
            notes.Add("Cover Urea / Ammonium Sulphate with soil after applying, or apply just before light rain. " +
                      "Do not apply on dry soil, on standing water, or just before a heavy storm — the nitrogen will be lost.");
        }

        if (used.Any(p => p.Type == FertilizerProductType.Organic))
        {
            notes.Add("Manure nutrient content varies widely. Use well-composted manure, apply it 2–3 weeks before planting and mix it into the soil.");
        }

        if (crop.Notes is { } cropNotes)
        {
            notes.Add(cropNotes);
        }

        if (used.Count > 0)
        {
            notes.Add("Place fertilizer 5–10 cm away from seeds and stems (in a band or ring) and cover it — direct contact can burn plants. " +
                      "Wear gloves and wash your hands after handling fertilizer; store it away from children, food and water.");
        }

        notes.Add("These rates are general guidelines based on regional averages and must be validated locally. " +
                  "For best results, use a soil test and ask your local agricultural extension officer.");
    }

    // ---------- Rounding / formatting ----------

    /// <summary>Rounds up to a multiple of <paramref name="increment"/> (e.g. 0.5 = nearest half bag). Zero stays zero.</summary>
    public static double RoundUpTo(double value, double increment)
    {
        if (value <= 0)
        {
            return 0;
        }

        return increment <= 0 ? value : Math.Round(Math.Ceiling(value / increment - 1e-9) * increment, 3);
    }

    private static double RoundToHalf(double value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;

    private static string Lower(SoilFertility fertility) => fertility.ToString().ToLowerInvariant();

    private static string F0(double value) => value.ToString("0", Invariant);

    private static string F1(double value) => value.ToString("0.#", Invariant);

    private static string F2(double value) => value.ToString("0.##", Invariant);
}
