using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Tests.Fertilizer;

public class FertilizerCalculatorServiceTests
{
    // A synthetic crop with round numbers so expected values are easy to verify by hand:
    // 100 N / 50 P2O5 / 50 K2O kg/ha at 4 t/ha; basal = 50% N + all P + all K; top-dress = other 50% N.
    private static FertilizerCropProfile TestCrop(double n = 100, double p = 50, double k = 50, int? plantsPerHa = 50_000) =>
        FertilizerCropProfile.Create(
            "test-crop", "Test crop", null, n, p, k,
            referenceYieldTPerHa: 4, maxRealisticYieldTPerHa: 6, yieldBasis: "grain",
            plantsPerHectare: plantsPerHa, isPerennial: false, notes: null,
            schedule:
            [
                new FertilizerSplit("Basal (at planting)", 0, "At planting", 50, 100, 100),
                new FertilizerSplit("Top-dressing", 35, "5 weeks after planting", 50, 0, 0)
            ]);

    private static readonly IReadOnlyList<FertilizerProduct> Catalogue = FertilizerReferenceData.Products();

    private static FertilizerCalculatorService Calculator(FertilizerCalculatorOptions? options = null) =>
        new(Options.Create(options ?? new FertilizerCalculatorOptions()));

    private static FertilizerCalculationRequest Request(
        double area = 1,
        AreaUnit unit = AreaUnit.Hectare,
        SoilInputDto? soil = null,
        double? targetYield = null,
        string[]? products = null,
        ProductPriceDto[]? prices = null,
        double? lengthM = null,
        double? widthM = null) =>
        new("test-crop", area, unit, lengthM, widthM,
            soil ?? new SoilInputDto(SoilInputMode.Simple, SoilFertility.Medium, Ph: 6.0),
            targetYield, products, prices);

    private static readonly string[] NpkAndStraights = ["npk-15-15-15", "urea", "tsp", "mop"];

    private static ProductQuantityDto Product(FertilizerCalculationResponseDto result, string id) =>
        result.Products.Single(product => product.ProductId == id);

    // ---------- 0. Unit conversions ----------

    [Theory]
    [InlineData(2.0, AreaUnit.Hectare, 2.0)]
    [InlineData(1.0, AreaUnit.Acre, 0.4047)]
    [InlineData(2.5, AreaUnit.Acre, 1.01175)]
    [InlineData(5000, AreaUnit.SquareMeter, 0.5)]
    [InlineData(10_000, AreaUnit.SquareMeter, 1.0)]
    public void AreaConverter_converts_to_hectares(double area, AreaUnit unit, double expectedHectares)
    {
        Assert.Equal(expectedHectares, AreaConverter.ToHectares(area, unit, null, null), 6);
    }

    [Fact]
    public void AreaConverter_uses_length_times_width_for_plot_dimensions()
    {
        Assert.Equal(0.2, AreaConverter.ToHectares(null, AreaUnit.PlotDimensions, 50, 40), 6);
    }

    [Fact]
    public void Calculate_scales_requirement_by_converted_area()
    {
        var result = Calculator().Calculate(Request(area: 1, unit: AreaUnit.Acre), TestCrop(), Catalogue);

        Assert.Equal(0.4047, result.AreaHectares, 4);
        Assert.Equal(40.5, result.NutrientRequirementKg.N); // 100 × 0.4047
        Assert.Equal(20.2, result.NutrientRequirementKg.P2O5); // 50 × 0.4047 = 20.235
    }

    [Fact]
    public void Calculate_accepts_plot_dimensions()
    {
        var result = Calculator().Calculate(
            Request(area: 0, unit: AreaUnit.PlotDimensions, lengthM: 20, widthM: 25), TestCrop(), Catalogue);

        Assert.Equal(0.05, result.AreaHectares, 4);
        Assert.Equal(5.0, result.NutrientRequirementKg.N);
    }

    // ---------- 1. Yield scaling and cap ----------

    [Fact]
    public void Missing_target_yield_uses_reference_yield()
    {
        var result = Calculator().Calculate(Request(), TestCrop(), Catalogue);

        Assert.Equal(4.0, result.TargetYieldTPerHa);
        Assert.False(result.TargetYieldCapped);
        Assert.Equal(new NutrientAmountsDto(100, 50, 50), result.NutrientRequirementPerHaKg);
    }

    [Fact]
    public void Requirement_scales_linearly_with_target_yield()
    {
        var result = Calculator().Calculate(Request(targetYield: 5), TestCrop(), Catalogue);

        Assert.Equal(new NutrientAmountsDto(125, 62.5, 62.5), result.NutrientRequirementPerHaKg);
    }

    [Fact]
    public void Target_above_max_realistic_yield_is_capped_with_warning()
    {
        var result = Calculator().Calculate(Request(targetYield: 10), TestCrop(), Catalogue);

        Assert.True(result.TargetYieldCapped);
        Assert.Equal(6.0, result.TargetYieldTPerHa);
        Assert.Equal(150, result.NutrientRequirementPerHaKg.N); // 100 × 6/4
        Assert.Contains(result.Warnings, w => w.Contains("above the realistic maximum"));
    }

    [Fact]
    public void Yield_scaling_factor_is_clamped_to_configured_limits()
    {
        var options = new FertilizerCalculatorOptions { MaxYieldScale = 1.2, MinYieldScale = 0.8 };

        var high = Calculator(options).Calculate(Request(targetYield: 6), TestCrop(), Catalogue);
        var low = Calculator(options).Calculate(Request(targetYield: 1), TestCrop(), Catalogue);

        Assert.Equal(120, high.NutrientRequirementPerHaKg.N);
        Assert.Equal(80, low.NutrientRequirementPerHaKg.N);
        Assert.Contains(high.Notes, n => n.Contains("limits yield scaling"));
    }

    // ---------- 2. Soil adjustment ----------

    [Theory]
    [InlineData(SoilFertility.Low, 125, 62.5)]
    [InlineData(SoilFertility.Medium, 100, 50)]
    [InlineData(SoilFertility.High, 60, 30)]
    public void Simple_mode_applies_fertility_multiplier(SoilFertility fertility, double expectedN, double expectedP)
    {
        var soil = new SoilInputDto(SoilInputMode.Simple, fertility);

        var result = Calculator().Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Equal(expectedN, result.NutrientRequirementPerHaKg.N);
        Assert.Equal(expectedP, result.NutrientRequirementPerHaKg.P2O5);
        Assert.Equal(expectedP, result.NutrientRequirementPerHaKg.K2O);
        Assert.Equal(fertility, result.SoilAdjustment.NitrogenClass);
    }

    [Fact]
    public void Fertility_multipliers_come_from_configuration()
    {
        var options = new FertilizerCalculatorOptions { SoilFertilityMultipliers = new SoilClassMultipliers { Low = 1.5 } };
        var soil = new SoilInputDto(SoilInputMode.Simple, SoilFertility.Low);

        var result = Calculator(options).Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Equal(150, result.NutrientRequirementPerHaKg.N);
    }

    [Fact]
    public void Sandy_soil_raises_nitrogen_only()
    {
        var soil = new SoilInputDto(SoilInputMode.Simple, SoilFertility.Medium, SoilType.Sandy);

        var result = Calculator().Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Equal(110, result.NutrientRequirementPerHaKg.N);
        Assert.Equal(50, result.NutrientRequirementPerHaKg.P2O5);
        Assert.Contains(result.Notes, n => n.Contains("Sandy soils"));
    }

    [Fact]
    public void Soil_test_mode_classifies_each_nutrient_independently()
    {
        // N 0.05% → low (×1.25); Bray-1 P 25 mg/kg → high (×0.6); K 0.3 cmol/kg → medium (×1.0)
        var soil = new SoilInputDto(SoilInputMode.SoilTest, Ph: 6.2,
            NitrogenTotalPercent: 0.05, PhosphorusBray1MgPerKg: 25, PotassiumCmolPerKg: 0.3);

        var result = Calculator().Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Equal(SoilInputMode.SoilTest, result.SoilAdjustment.Mode);
        Assert.Equal(SoilFertility.Low, result.SoilAdjustment.NitrogenClass);
        Assert.Equal(SoilFertility.High, result.SoilAdjustment.PhosphorusClass);
        Assert.Equal(SoilFertility.Medium, result.SoilAdjustment.PotassiumClass);
        Assert.Equal(new NutrientAmountsDto(125, 30, 50), result.NutrientRequirementPerHaKg);
    }

    [Fact]
    public void Soil_test_mode_is_inferred_when_mode_is_omitted()
    {
        var soil = new SoilInputDto(NitrogenTotalPercent: 0.25, PhosphorusBray1MgPerKg: 5, PotassiumCmolPerKg: 0.5);

        var result = Calculator().Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Equal(SoilInputMode.SoilTest, result.SoilAdjustment.Mode);
        Assert.Equal(new NutrientAmountsDto(60, 62.5, 30), result.NutrientRequirementPerHaKg);
    }

    [Fact]
    public void Requirement_never_goes_below_zero()
    {
        var options = new FertilizerCalculatorOptions { SoilFertilityMultipliers = new SoilClassMultipliers { High = -0.5 } };
        var soil = new SoilInputDto(SoilInputMode.Simple, SoilFertility.High);

        var result = Calculator(options).Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Equal(new NutrientAmountsDto(0, 0, 0), result.NutrientRequirementKg);
        Assert.Empty(result.Products);
        Assert.Contains(result.Notes, n => n.Contains("No fertilizer is needed"));
    }

    // ---------- 4. Limiting nutrient + straight fill ----------

    [Fact]
    public void Compound_is_applied_up_to_the_limiting_nutrient_then_urea_fills_nitrogen()
    {
        // NPK 15-15-15: P and K (50 kg) limit it to 50 / 0.15 = 333.3 kg, which also gives 50 N.
        // Urea then supplies the other 50 N: 50 / 0.46 = 108.7 kg. TSP and MOP are not needed.
        var result = Calculator().Calculate(Request(products: NpkAndStraights), TestCrop(), Catalogue);

        Assert.Equal(FertilizerSelectionMode.FarmerSelected, result.SelectionMode);
        Assert.Equal(["npk-15-15-15", "urea"], result.Products.Select(p => p.ProductId));
        Assert.Equal(333.3, Product(result, "npk-15-15-15").QuantityKg);
        Assert.Equal(108.7, Product(result, "urea").QuantityKg);
        Assert.Equal(new NutrientAmountsDto(100, 50, 50), result.NutrientsSupplied);
        Assert.Equal(new NutrientAmountsDto(0, 0, 0), result.NutrientBalanceKg);
        Assert.Contains(result.Notes, n => n.StartsWith("TSP (0-46-0) was not needed"));
    }

    [Fact]
    public void Compound_limited_by_potassium_gets_phosphate_and_nitrogen_top_ups()
    {
        // K 30 limits NPK to 200 kg (30/30/30). Then TSP: 20 / 0.46 = 43.5 kg; Urea: 70 / 0.46 = 152.2 kg.
        var result = Calculator().Calculate(Request(products: NpkAndStraights), TestCrop(k: 30), Catalogue);

        Assert.Equal(200.0, Product(result, "npk-15-15-15").QuantityKg);
        Assert.Equal(43.5, Product(result, "tsp").QuantityKg);
        Assert.Equal(152.2, Product(result, "urea").QuantityKg);
        Assert.DoesNotContain(result.Products, p => p.ProductId == "mop");
        Assert.Equal(new NutrientAmountsDto(100, 50, 30), result.NutrientsSupplied);
    }

    [Fact]
    public void Compound_is_skipped_when_any_of_its_nutrients_is_not_needed()
    {
        var result = Calculator().Calculate(Request(products: ["npk-15-15-15", "urea", "tsp"]), TestCrop(k: 0), Catalogue);

        Assert.DoesNotContain(result.Products, p => p.ProductId == "npk-15-15-15");
        Assert.Contains(result.Notes, n => n.StartsWith("NPK 15-15-15 was not used"));
        Assert.Equal(new NutrientAmountsDto(100, 50, 0), result.NutrientsSupplied);
    }

    [Fact]
    public void Nitrogen_supplied_by_DAP_is_counted_before_urea_is_sized()
    {
        // DAP for 46 P2O5 = 100 kg, which also brings 18 N. Urea covers only the remaining 82 N: 178.3 kg.
        var result = Calculator().Calculate(Request(products: ["dap", "urea", "mop"]), TestCrop(p: 46, k: 60), Catalogue);

        Assert.Equal(100.0, Product(result, "dap").QuantityKg);
        Assert.Equal(18.0, Product(result, "dap").NutrientsSupplied.N);
        Assert.Equal(178.3, Product(result, "urea").QuantityKg);
        Assert.Equal(100.0, Product(result, "mop").QuantityKg);
        Assert.Equal(100.0, result.NutrientsSupplied.N);
    }

    [Fact]
    public void Nitrogen_over_supplied_by_DAP_raises_a_warning()
    {
        // 100 kg DAP for 46 P2O5 brings 18 N against a 10 N requirement.
        var result = Calculator().Calculate(Request(products: ["dap"]), TestCrop(n: 10, p: 46, k: 0), Catalogue);

        Assert.Equal(8.0, result.NutrientBalanceKg.N);
        Assert.Contains(result.Warnings, w => w.StartsWith("N is over-supplied by 8 kg"));
    }

    [Fact]
    public void Automatic_selection_defaults_to_NPK_15_15_15_plus_urea_when_prices_are_unknown()
    {
        // Even though NPK 20-10-10 alone matches 100-50-50 exactly, the standard NPK 15-15-15 + Urea is used.
        var result = Calculator().Calculate(Request(products: []), TestCrop(), Catalogue);

        Assert.Equal(FertilizerSelectionMode.Automatic, result.SelectionMode);
        Assert.Equal(["npk-15-15-15", "urea"], result.Products.Select(p => p.ProductId));
        Assert.Equal(333.3, Product(result, "npk-15-15-15").QuantityKg);
        Assert.Equal(108.7, Product(result, "urea").QuantityKg);
        Assert.Empty(result.Warnings);
        Assert.Contains(result.Notes, n => n.StartsWith("No fertilizers were selected, so the standard recommendation was used"));
    }

    [Fact]
    public void Automatic_default_tops_up_with_straights_only_when_NPK_and_urea_cannot_cover_the_need()
    {
        // 60-40-80: NPK 15-15-15 is limited by P (266.7 kg → 40/40/40); MOP adds the extra 40 K2O, Urea the extra 20 N.
        var kHeavy = Calculator().Calculate(Request(products: []), TestCrop(n: 60, p: 40, k: 80), Catalogue);
        // 60-60-60: NPK 15-15-15 alone covers everything, so no Urea is added.
        var balanced = Calculator().Calculate(Request(products: []), TestCrop(n: 60, p: 60, k: 60), Catalogue);

        Assert.Equal(["npk-15-15-15", "mop", "urea"], kHeavy.Products.Select(p => p.ProductId));
        Assert.Equal(new NutrientAmountsDto(60, 40, 80), kHeavy.NutrientsSupplied);
        Assert.Equal(["npk-15-15-15"], balanced.Products.Select(p => p.ProductId));
    }

    [Fact]
    public void Automatic_selection_prefers_the_cheapest_plan_when_all_prices_are_known()
    {
        ProductPriceDto[] prices =
        [
            new("npk-15-15-15", 2000), new("npk-20-10-10", 2000), new("urea", 500),
            new("tsp", 500), new("mop", 500), new("dap", 500)
        ];

        var result = Calculator().Calculate(Request(prices: prices), TestCrop(), Catalogue);

        // DAP + MOP + Urea = 2.5×500 + 2×500 + 3.5×500 = 4000 SLE — cheaper than any NPK plan.
        Assert.Equal(["dap", "mop", "urea"], result.Products.Select(p => p.ProductId));
        Assert.Equal(4000m, result.TotalEstimatedCost);
        Assert.True(result.CostComplete);
    }

    // ---------- Missing-nutrient warnings ----------

    [Fact]
    public void Missing_potassium_source_is_reported_with_deficit()
    {
        var result = Calculator().Calculate(Request(products: ["urea", "tsp"]), TestCrop(), Catalogue);

        Assert.Contains(result.Warnings, w => w.StartsWith("No potassium source selected — K2O deficit of 50 kg"));
        Assert.Equal(-50, result.NutrientBalanceKg.K2O);
    }

    [Fact]
    public void Selected_products_that_cannot_cover_a_nutrient_are_reported()
    {
        // Only NPK: P/K limit it to 333.3 kg, leaving a 50 kg N gap and no straight N source.
        var result = Calculator().Calculate(Request(products: ["npk-15-15-15"]), TestCrop(), Catalogue);

        Assert.Contains(result.Warnings, w => w.StartsWith("Your selected fertilizers cannot fully supply nitrogen (N) — N deficit of 50 kg"));
    }

    // ---------- 5. Bag rounding ----------

    [Theory]
    [InlineData(333.33, 7.0)]  // 6.67 bags → 7
    [InlineData(108.7, 2.5)]   // 2.17 bags → 2.5
    [InlineData(150, 3.0)]     // exactly 3 bags stays 3
    [InlineData(10, 0.5)]      // any amount needs at least half a bag
    [InlineData(0, 0.0)]
    public void Bags_round_up_to_nearest_half_bag(double kg, double expectedBags)
    {
        Assert.Equal(expectedBags, FertilizerCalculatorService.RoundUpTo(kg / 50, 0.5));
    }

    [Fact]
    public void Response_reports_kg_to_one_decimal_and_half_bags()
    {
        var result = Calculator().Calculate(Request(products: NpkAndStraights), TestCrop(), Catalogue);

        var npk = Product(result, "npk-15-15-15");
        Assert.Equal(333.3, npk.QuantityKg);
        Assert.Equal(7.0, npk.Bags50Kg);
        Assert.Equal(7.0, npk.Bags);
        Assert.Equal(2.5, Product(result, "urea").Bags50Kg);
    }

    [Fact]
    public void Less_than_half_a_bag_gets_a_buy_loose_note()
    {
        var result = Calculator().Calculate(Request(area: 0.05, products: NpkAndStraights), TestCrop(), Catalogue);

        Assert.Equal(0.5, Product(result, "urea").Bags50Kg);
        Assert.Contains(result.Notes, n => n.Contains("less than half a bag"));
    }

    // ---------- 6. Schedule ----------

    [Fact]
    public void Schedule_puts_compound_at_basal_and_urea_at_top_dressing()
    {
        var result = Calculator().Calculate(Request(products: NpkAndStraights), TestCrop(), Catalogue);

        Assert.Equal(2, result.Schedule.Count);
        var basal = result.Schedule[0];
        var topDress = result.Schedule[1];

        Assert.Equal(0, basal.DaysAfterPlanting);
        Assert.Equal(["npk-15-15-15"], basal.Products.Select(p => p.ProductId));
        Assert.Equal(new NutrientAmountsDto(50, 50, 50), basal.Nutrients);

        Assert.Equal(35, topDress.DaysAfterPlanting);
        Assert.Equal(["urea"], topDress.Products.Select(p => p.ProductId));
        Assert.Equal(108.7, topDress.Products[0].QuantityKg);
    }

    [Fact]
    public void Schedule_gives_per_plant_amounts_when_population_is_known()
    {
        var result = Calculator().Calculate(Request(products: NpkAndStraights), TestCrop(), Catalogue);

        // 333.3 kg over 50,000 plants ≈ 6.7 g → about 1.5 bottle caps.
        var npk = result.Schedule[0].Products[0];
        Assert.Equal(50_000, result.PlantCount);
        Assert.Equal(6.7, npk.GramsPerPlant);
        Assert.StartsWith("about 1.5 level bottle caps per plant", npk.PerPlantHint);
    }

    [Fact]
    public void Schedule_omits_per_plant_amounts_when_population_is_unknown()
    {
        var result = Calculator().Calculate(Request(products: NpkAndStraights), TestCrop(plantsPerHa: null), Catalogue);

        Assert.Equal(0, result.PlantCount);
        Assert.All(result.Schedule.SelectMany(s => s.Products), p =>
        {
            Assert.Equal(0, p.GramsPerPlant);
            Assert.Equal("", p.PerPlantHint);
        });
    }

    // ---------- 7. Cost ----------

    [Fact]
    public void Cost_is_bags_times_price_when_all_prices_are_known()
    {
        ProductPriceDto[] prices = [new("npk-15-15-15", 950), new("urea", 900)];

        var result = Calculator().Calculate(Request(products: NpkAndStraights, prices: prices), TestCrop(), Catalogue);

        Assert.Equal(6650m, Product(result, "npk-15-15-15").EstimatedCost); // 7 × 950
        Assert.Equal(2250m, Product(result, "urea").EstimatedCost);         // 2.5 × 900
        Assert.Equal(8900m, result.TotalEstimatedCost);
        Assert.True(result.CostComplete);
        Assert.Equal("SLE", result.Currency);
    }

    [Fact]
    public void Cost_is_partial_when_some_prices_are_missing()
    {
        ProductPriceDto[] prices = [new("npk-15-15-15", 950)];

        var result = Calculator().Calculate(Request(products: NpkAndStraights, prices: prices), TestCrop(), Catalogue);

        var urea = Product(result, "urea");
        Assert.False(urea.HasPrice);
        Assert.Equal(0m, urea.EstimatedCost);
        Assert.Equal(6650m, result.TotalEstimatedCost);
        Assert.False(result.CostComplete);
        Assert.Contains(result.Notes, n => n.Contains("excludes Urea (46-0-0)"));
    }

    [Fact]
    public void Stored_product_price_is_used_when_no_custom_price_is_given()
    {
        var urea = FertilizerProduct.Create("urea", "Urea (46-0-0)", FertilizerProductType.Straight, 46, 0, 0, pricePerBag: 800m);

        var result = Calculator().Calculate(Request(products: ["urea"]), TestCrop(p: 0, k: 0), [urea]);

        Assert.Equal(4.5, result.Products[0].Bags); // 217.4 kg
        Assert.Equal(3600m, result.TotalEstimatedCost);
    }

    // ---------- 8. Notes ----------

    [Fact]
    public void Acidic_soil_gets_liming_advice()
    {
        var soil = new SoilInputDto(SoilInputMode.Simple, SoilFertility.Medium, Ph: 5.0);

        var result = Calculator().Calculate(Request(soil: soil), TestCrop(), Catalogue);

        Assert.Contains(result.Notes, n => n.Contains("agricultural lime"));
    }

    [Fact]
    public void Neutral_soil_gets_no_liming_advice_and_always_a_disclaimer()
    {
        var result = Calculator().Calculate(Request(), TestCrop(), Catalogue);

        Assert.DoesNotContain(result.Notes, n => n.Contains("agricultural lime"));
        Assert.Contains("must be validated locally", result.Notes[^1]);
    }

    [Fact]
    public void Very_high_rates_raise_an_over_application_warning()
    {
        var options = new FertilizerCalculatorOptions { MaxMineralProductKgPerHa = 300 };

        var result = Calculator(options).Calculate(Request(products: NpkAndStraights), TestCrop(), Catalogue);

        Assert.Contains(result.Warnings, w => w.Contains("kg/ha of mineral fertilizer"));
    }

    // ---------- Validation ----------

    [Theory]
    [InlineData(0.0, "area")]
    [InlineData(-1.0, "area")]
    public void Non_positive_area_is_rejected(double area, string field)
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Calculator().Calculate(Request(area: area), TestCrop(), Catalogue));

        Assert.True(exception.Errors!.ContainsKey(field));
    }

    [Fact]
    public void Invalid_inputs_are_all_reported_by_field()
    {
        var request = new FertilizerCalculationRequest(
            "test-crop", 1, (AreaUnit)99, Soil: null, FertilizerProductIds: ["npk-99-99-99"]);

        var exception = Assert.Throws<ValidationException>(() => Calculator().Calculate(request, TestCrop(), Catalogue));

        Assert.Contains("areaUnit", exception.Errors!.Keys);
        Assert.Contains("soil", exception.Errors.Keys);
        Assert.Contains("fertilizerProductIds[0]", exception.Errors.Keys);
    }

    [Fact]
    public void Negative_soil_test_values_are_rejected()
    {
        var soil = new SoilInputDto(SoilInputMode.SoilTest, NitrogenTotalPercent: 0.1, PhosphorusBray1MgPerKg: -3, PotassiumCmolPerKg: 0.2);

        var exception = Assert.Throws<ValidationException>(() =>
            Calculator().Calculate(Request(soil: soil), TestCrop(), Catalogue));

        Assert.Equal(["soil.phosphorusBray1MgPerKg"], exception.Errors!.Keys);
    }

    [Fact]
    public void Simple_mode_requires_fertility_and_plot_mode_requires_dimensions()
    {
        var request = Request(unit: AreaUnit.PlotDimensions, lengthM: 20, soil: new SoilInputDto(SoilInputMode.Simple));

        var exception = Assert.Throws<ValidationException>(() => Calculator().Calculate(request, TestCrop(), Catalogue));

        Assert.Contains("widthM", exception.Errors!.Keys);
        Assert.Contains("soil.fertility", exception.Errors.Keys);
    }

    // ---------- Seed data ----------

    [Fact]
    public void Seed_data_contains_every_required_crop_and_product()
    {
        var crops = FertilizerReferenceData.Crops().Select(c => c.Key).ToList();
        var products = FertilizerReferenceData.Products().Select(p => p.Key).ToList();

        Assert.Equal(
            ["rice-upland", "rice-lowland", "maize", "cassava", "sweet-potato", "groundnut", "pepper-chili", "tomato", "okra", "cocoa", "oil-palm"],
            crops);
        Assert.Equal(
            ["npk-15-15-15", "npk-20-10-10", "urea", "dap", "tsp", "ssp", "mop", "ammonium-sulphate", "poultry-manure"],
            products);
        Assert.Equal(FertilizerProductType.Organic, FertilizerReferenceData.Products().Single(p => p.Key == "poultry-manure").Type);
    }

    [Fact]
    public void Every_seed_crop_gets_a_complete_automatic_plan()
    {
        foreach (var crop in FertilizerReferenceData.Crops())
        {
            var request = Request() with { CropId = crop.Key };

            var result = Calculator().Calculate(request, crop, Catalogue);

            Assert.DoesNotContain(result.Warnings, w => w.Contains("deficit"));
            Assert.NotEmpty(result.Schedule);
            Assert.Equal(result.Products.Sum(p => p.QuantityKg),
                result.Schedule.SelectMany(s => s.Products).Sum(p => p.QuantityKg), tolerance: 0.5);
        }
    }
}
