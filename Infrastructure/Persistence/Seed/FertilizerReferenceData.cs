using AgrismartAPI.Domain.Fertilizer;

namespace AgrismartAPI.Infrastructure.Persistence.Seed;

// =====================================================================================================
//  FERTILIZER REFERENCE DATA — FOR AGRONOMIST REVIEW
// =====================================================================================================
//
//  !!  These values are GENERAL GUIDELINES compiled from regional (West African / Sierra Leonean)   !!
//  !!  extension recommendations and textbook nutrient-uptake figures. They are NOT calibrated for  !!
//  !!  any specific variety, district or soil. They MUST be reviewed and validated locally (e.g. by  !!
//  !!  SLARI / MAF extension staff) before farmers rely on them.                                    !!
//
//  How these values are used
//  -------------------------
//  * This file is only read by AppDbContextSeeder, and only when the FertilizerCrops /
//    FertilizerProducts tables are EMPTY (first start-up after the migration).
//  * After that, change values through the admin endpoints (no redeploy needed):
//        PUT /fertilizer/crops/{id}      PUT /fertilizer/products/{id}
//    Editing this file later only affects NEW databases.
//  * Calculator multipliers and thresholds (soil factors, soil-test class limits, liming pH,
//    yield-scaling limits, bag rounding, ...) are in appsettings.json → "FertilizerCalculator".
//
//  Crop columns
//  ------------
//  N / P2O5 / K2O  kg per hectare needed to reach the REFERENCE yield on MEDIUM-fertility soil.
//                  P and K are expressed as oxides, as printed on fertilizer bags.
//  RefYield        t/ha the rates above are calibrated for. Requirement scales linearly with
//                  TargetYield / RefYield (limited by MinYieldScale/MaxYieldScale in appsettings).
//  MaxYield        Highest target yield accepted; higher targets are capped with a warning.
//  Plants/ha       Typical plant (or stand) population, used only for grams-per-plant hints.
//                  null = broadcast / drilled crop where per-plant amounts make no sense.
//  Schedule        Split applications. For each nutrient, the percentages across all splits
//                  MUST add up to 100. DaysAfterPlanting = days after planting / transplanting;
//                  for perennials it means days after the start of the rainy season.
//
//  Local (Krio) names are best-effort and should be checked by a native speaker.
// =====================================================================================================

public static class FertilizerReferenceData
{
    public static IReadOnlyList<FertilizerCropProfile> Crops() =>
    [
        // ---------------- Cereals ----------------

        // Upland rice: rain-fed, lower yield potential. Drilled/broadcast, so no per-plant hints.
        FertilizerCropProfile.Create(
            key: "rice-upland", name: "Rice (upland)", localName: "Res",
            nitrogenKgPerHa: 60, phosphateKgPerHa: 30, potashKgPerHa: 30,
            referenceYieldTPerHa: 2.0, maxRealisticYieldTPerHa: 4.0, yieldBasis: "paddy",
            plantsPerHectare: null, isPerennial: false,
            notes: "Upland rice: weed before every fertilizer application — weeds take the fertilizer first.",
            schedule:
            [
                new("Basal (at planting)", 0, "At sowing, worked into the soil", 34, 100, 100),
                new("Top-dressing 1 (tillering)", 30, "About 4 weeks after sowing", 33, 0, 0),
                new("Top-dressing 2 (panicle initiation)", 55, "About 8 weeks after sowing", 33, 0, 0)
            ]),

        // Lowland rice in inland valley swamps (IVS): water control allows higher yields.
        FertilizerCropProfile.Create(
            key: "rice-lowland", name: "Rice (lowland / IVS)", localName: "Res",
            nitrogenKgPerHa: 80, phosphateKgPerHa: 40, potashKgPerHa: 40,
            referenceYieldTPerHa: 3.5, maxRealisticYieldTPerHa: 6.0, yieldBasis: "paddy",
            plantsPerHectare: null, isPerennial: false,
            notes: "Lowland rice: drain the field to shallow water before top-dressing urea and keep the water in for a few days afterwards; " +
                   "urea spread on deep flowing water is lost.",
            schedule:
            [
                new("Basal (at transplanting)", 0, "At final puddling / transplanting", 30, 100, 100),
                new("Top-dressing 1 (tillering)", 21, "About 3 weeks after transplanting", 40, 0, 0),
                new("Top-dressing 2 (panicle initiation)", 45, "About 6-7 weeks after transplanting", 30, 0, 0)
            ]),

        // Maize: heavy N feeder; one basal + one top-dressing is standard practice.
        FertilizerCropProfile.Create(
            key: "maize", name: "Maize", localName: "Kon",
            nitrogenKgPerHa: 90, phosphateKgPerHa: 45, potashKgPerHa: 45,
            referenceYieldTPerHa: 3.5, maxRealisticYieldTPerHa: 7.0, yieldBasis: "dry grain",
            plantsPerHectare: 53_333, // 75 cm x 25 cm, 1 plant per stand
            isPerennial: false,
            notes: null,
            schedule:
            [
                new("Basal (at planting)", 0, "At planting or within 1 week, in a band beside the row", 40, 100, 100),
                new("Top-dressing", 35, "4-6 weeks after planting, before tasselling", 60, 0, 0)
            ]),

        // ---------------- Roots and tubers ----------------

        // Cassava: K is the key nutrient for root bulking. Applied after establishment, not at planting.
        FertilizerCropProfile.Create(
            key: "cassava", name: "Cassava", localName: "Kasada",
            nitrogenKgPerHa: 60, phosphateKgPerHa: 40, potashKgPerHa: 80,
            referenceYieldTPerHa: 15, maxRealisticYieldTPerHa: 35, yieldBasis: "fresh roots",
            plantsPerHectare: 10_000, // 1 m x 1 m
            isPerennial: false,
            notes: "Cassava: apply in a ring 10-15 cm from the stem once cuttings have sprouted; do not put fertilizer in the planting hole.",
            schedule:
            [
                new("First application", 30, "4-6 weeks after planting, once sprouted", 50, 100, 50),
                new("Second application", 90, "About 3 months after planting", 50, 0, 50)
            ]),

        FertilizerCropProfile.Create(
            key: "sweet-potato", name: "Sweet potato", localName: "Petete",
            nitrogenKgPerHa: 45, phosphateKgPerHa: 45, potashKgPerHa: 90,
            referenceYieldTPerHa: 12, maxRealisticYieldTPerHa: 30, yieldBasis: "fresh roots",
            plantsPerHectare: 33_333, // 100 cm ridges x 30 cm
            isPerennial: false,
            notes: "Sweet potato: too much nitrogen gives many leaves and few roots — do not exceed the recommended N.",
            schedule:
            [
                new("Basal (at planting)", 0, "At planting, mixed into the ridge", 50, 100, 50),
                new("Top-dressing", 42, "About 6 weeks after planting", 50, 0, 50)
            ]),

        // ---------------- Legumes ----------------

        // Groundnut fixes most of its own N; a small starter dose only. Needs P and calcium.
        FertilizerCropProfile.Create(
            key: "groundnut", name: "Groundnut", localName: "Granat",
            nitrogenKgPerHa: 20, phosphateKgPerHa: 40, potashKgPerHa: 30,
            referenceYieldTPerHa: 1.5, maxRealisticYieldTPerHa: 3.0, yieldBasis: "dry pods",
            plantsPerHectare: null, isPerennial: false,
            notes: "Groundnut fixes its own nitrogen, so only a small starter dose is given. SSP is a good P source because it also supplies " +
                   "sulphur and calcium, which help pod filling.",
            schedule:
            [
                new("Basal (at planting)", 0, "At planting, in a band 5 cm from the seed row", 100, 100, 100)
            ]),

        // ---------------- Vegetables ----------------

        FertilizerCropProfile.Create(
            key: "pepper-chili", name: "Pepper (hot / chili)", localName: "Pepe",
            nitrogenKgPerHa: 90, phosphateKgPerHa: 60, potashKgPerHa: 90,
            referenceYieldTPerHa: 10, maxRealisticYieldTPerHa: 20, yieldBasis: "fresh fruit",
            plantsPerHectare: 26_667, // 75 cm x 50 cm
            isPerennial: false,
            notes: null,
            schedule:
            [
                new("Basal (at transplanting)", 0, "At transplanting, mixed into the planting station", 30, 100, 40),
                new("Top-dressing 1 (flowering)", 30, "About 4 weeks after transplanting", 35, 0, 30),
                new("Top-dressing 2 (fruiting)", 60, "About 8 weeks after transplanting", 35, 0, 30)
            ]),

        FertilizerCropProfile.Create(
            key: "tomato", name: "Tomato", localName: "Tomatis",
            nitrogenKgPerHa: 120, phosphateKgPerHa: 60, potashKgPerHa: 120,
            referenceYieldTPerHa: 20, maxRealisticYieldTPerHa: 40, yieldBasis: "fresh fruit",
            plantsPerHectare: 26_667, // 75 cm x 50 cm
            isPerennial: false,
            notes: "Tomato: steady watering prevents blossom-end rot; potassium improves fruit quality.",
            schedule:
            [
                new("Basal (at transplanting)", 0, "At transplanting, mixed into the planting station", 30, 100, 40),
                new("Top-dressing 1 (flowering)", 30, "About 4 weeks after transplanting", 35, 0, 30),
                new("Top-dressing 2 (fruiting)", 60, "About 8 weeks after transplanting", 35, 0, 30)
            ]),

        FertilizerCropProfile.Create(
            key: "okra", name: "Okra", localName: "Okra",
            nitrogenKgPerHa: 80, phosphateKgPerHa: 40, potashKgPerHa: 40,
            referenceYieldTPerHa: 8, maxRealisticYieldTPerHa: 15, yieldBasis: "fresh pods",
            plantsPerHectare: 55_555, // 60 cm x 30 cm
            isPerennial: false,
            notes: null,
            schedule:
            [
                new("Basal (at planting)", 0, "At sowing, in a band beside the row", 50, 100, 100),
                new("Top-dressing", 30, "About 4 weeks after sowing, at first flowering", 50, 0, 0)
            ]),

        // ---------------- Tree crops (rates are per hectare PER YEAR for a MATURE plantation) ----------------

        FertilizerCropProfile.Create(
            key: "cocoa", name: "Cocoa (mature)", localName: "Koko",
            nitrogenKgPerHa: 30, phosphateKgPerHa: 40, potashKgPerHa: 60,
            referenceYieldTPerHa: 0.5, maxRealisticYieldTPerHa: 1.5, yieldBasis: "dry beans",
            plantsPerHectare: 1_111, // 3 m x 3 m
            isPerennial: true,
            notes: "Cocoa: rates are per year for bearing trees (3+ years). Spread in a ring 30-100 cm from the trunk, under the canopy. " +
                   "Fertilizer only pays off where shade, weeds, pests and diseases (e.g. black pod) are also managed.",
            schedule:
            [
                new("Early rainy season", 0, "Start of the rains (about April-May)", 50, 50, 50),
                new("Mid rainy season", 90, "About 3 months later (July-August)", 50, 50, 50)
            ]),

        FertilizerCropProfile.Create(
            key: "oil-palm", name: "Oil palm (mature)", localName: "Pam",
            nitrogenKgPerHa: 70, phosphateKgPerHa: 35, potashKgPerHa: 170,
            referenceYieldTPerHa: 15, maxRealisticYieldTPerHa: 25, yieldBasis: "fresh fruit bunches",
            plantsPerHectare: 143, // 9 m triangular
            isPerennial: true,
            notes: "Oil palm: rates are per year for bearing palms. Spread evenly in the weeded circle 1-2 m from the trunk. " +
                   "Potassium is the most important nutrient for bunch yield.",
            schedule:
            [
                new("Early rainy season", 0, "Start of the rains (about April-May)", 50, 100, 50),
                new("Late rainy season", 120, "About 4 months later (August-September)", 50, 0, 50)
            ])
    ];

    // Product analyses are standard label values (% N - % P2O5 - % K2O).
    // Prices are left EMPTY on purpose: set current local prices (SLE per bag) with
    // PUT /fertilizer/products/{id}, or farmers can send their own prices in each request.
    public static IReadOnlyList<FertilizerProduct> Products() =>
    [
        FertilizerProduct.Create("npk-15-15-15", "NPK 15-15-15", FertilizerProductType.Compound, 15, 15, 15,
            notes: "Balanced compound — the most widely available fertilizer in Sierra Leone."),
        FertilizerProduct.Create("npk-20-10-10", "NPK 20-10-10", FertilizerProductType.Compound, 20, 10, 10,
            notes: "Nitrogen-rich compound, suits cereals."),
        FertilizerProduct.Create("urea", "Urea (46-0-0)", FertilizerProductType.Straight, 46, 0, 0,
            notes: "Most concentrated N source. Cover with soil after applying."),
        FertilizerProduct.Create("dap", "DAP (18-46-0)", FertilizerProductType.Compound, 18, 46, 0,
            notes: "Diammonium phosphate — P source that also supplies N."),
        FertilizerProduct.Create("tsp", "TSP (0-46-0)", FertilizerProductType.Straight, 0, 46, 0,
            notes: "Triple superphosphate."),
        FertilizerProduct.Create("ssp", "SSP (0-20-0)", FertilizerProductType.Straight, 0, 20, 0, sulphurPercent: 12,
            notes: "Single superphosphate — also supplies sulphur (~12%) and calcium."),
        FertilizerProduct.Create("mop", "MOP / Muriate of Potash (0-0-60)", FertilizerProductType.Straight, 0, 0, 60,
            notes: "Potassium chloride."),
        FertilizerProduct.Create("ammonium-sulphate", "Ammonium Sulphate (21-0-0)", FertilizerProductType.Straight, 21, 0, 0,
            sulphurPercent: 24, notes: "N source with sulphur (~24%); slightly acidifying — lime acid soils."),
        FertilizerProduct.Create("poultry-manure", "Poultry Manure (approx. 3-2-2)", FertilizerProductType.Organic, 3, 2, 2,
            notes: "Organic. Analysis varies a lot with feed, bedding and storage; values are approximate (dry, composted).")
    ];
}
