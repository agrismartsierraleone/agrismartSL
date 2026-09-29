# Fertilizer Recommendation and Calculator

Works out how much fertilizer a farmer should buy and apply to a whole farm or a single plot, based on
**crop**, **area**, **soil condition**, **target yield** and the **fertilizers the farmer has**. Answers
are given in kilograms, 50 kg bags, per-plant amounts, a split application schedule and a cost estimate.

> ⚠️ All agronomic values are **general guidelines** and must be validated locally before farmers rely on them.

Frontend integration guide: [README.fertilizer.mobile.md](README.fertilizer.mobile.md) · Sample requests: [fertilizer.http](fertilizer.http)

## Where things live

| Concern | File |
|---|---|
| Crop & product reference values (agronomist-reviewable) | `Infrastructure/Persistence/Seed/FertilizerReferenceData.cs` |
| Multipliers & thresholds | `appsettings.json` → `"FertilizerCalculator"` (bound to `Application/Fertilizer/FertilizerCalculatorOptions.cs`) |
| Calculation logic (pure, unit-tested) | `Application/Fertilizer/FertilizerCalculatorService.cs` (`IFertilizerCalculatorService`) |
| Input validation | `Application/Fertilizer/FertilizerRequestValidator.cs` |
| Endpoints | `Controllers/FertilizerController.cs` → `/fertilizer/...` |
| Tables | `FertilizerCrops`, `FertilizerProducts`, `FertilizerRecommendations` (migration `AddFertilizerCalculator`) |
| Tests | `tests/AgrismartAPI.Tests/Fertilizer/` |

## Endpoints

| Method | Route | Auth |
|---|---|---|
| GET | `/fertilizer/crops`, `/fertilizer/crops/{id}` | anonymous |
| GET | `/fertilizer/products`, `/fertilizer/products/{id}` | anonymous |
| POST | `/fertilizer/calculate` | anonymous — stateless, nothing saved |
| POST | `/fertilizer/recommendations` | signed in — calculate **and** save |
| GET | `/fertilizer/recommendations?cropId=&plotLabel=&userId=&page=&pageSize=` | signed in — own history; admins may pass `userId` or see everyone |
| GET / DELETE | `/fertilizer/recommendations/{id}` | owner or admin (others get 404) |
| POST / PUT | `/fertilizer/crops[/{id}]`, `/fertilizer/products[/{id}]` | Admin |

Routes follow the API's existing unprefixed, unversioned style (`/crops`, `/harvest/predict`, …).
Ids for crops and products are stable lowercase keys (`maize`, `npk-15-15-15`), like the harvest crop catalog.

## The formulas

All amounts are kg of nutrient; P and K are expressed as oxides (P₂O₅, K₂O), as printed on bags.

1. **Area → hectares.** 1 acre = 0.4047 ha; 1 ha = 10,000 m²; plot = length × width ÷ 10,000.
2. **Yield scaling.**
   `target = min(TargetYield ?? ReferenceYield, MaxRealisticYield)` (warning if capped)
   `scale = clamp(target ÷ ReferenceYield, MinYieldScale, MaxYieldScale)` (default 0.5–2.0)
   `requirement/ha = base requirement/ha × scale`
3. **Soil adjustment** (a multiplier per nutrient, floored at 0):
   * *Simple mode:* Low ×1.25 · Medium ×1.0 · High ×0.6 on N, P₂O₅ and K₂O.
   * *Soil-test mode:* each nutrient is classed separately and gets its class multiplier (the "soil supply credit" — a High class means the soil already supplies ~40 % of the demand):

     | Nutrient | Unit / method assumed | Low if below | High if above |
     |---|---|---|---|
     | N | total N, % (Kjeldahl) | 0.10 | 0.20 |
     | P | available P, mg/kg (**Bray-1** — suits acid soils) | 10 | 20 |
     | K | exchangeable K, cmol(+)/kg (1 M NH₄OAc, pH 7) | 0.20 | 0.40 |
   * *Soil type* (either mode) adds an N-only multiplier: Sandy ×1.1 (leaching), Loamy/Clay ×1.0.
4. **Total** = requirement/ha × area (ha).
5. **Products.** `product kg = nutrient kg ÷ (nutrient % ÷ 100)`
   1. **Base compound (limiting nutrient).** Among selected products carrying *all three* nutrients (NPK blends, manure), take the one that delivers the most nutrient at
      `kg = min over its nutrients of (remaining nutrient ÷ its %)` — the most you can apply without overshooting any nutrient it contains. If any of its nutrients is not needed, it is skipped (with a note).
   2. **Straight fill,** in the order **P → K → N**: TSP/SSP/DAP for P, MOP for K, then Urea/Ammonium Sulphate for N. Filling N last means **N carried by DAP is already counted** before Urea is sized. Among several candidates for a nutrient, the one causing the least over-supply wins, then the most concentrated.
   3. Any nutrient still short → warning, e.g. *"No potassium source selected — K2O deficit of 50 kg. Add MOP (Muriate of Potash)."* Over-supply beyond `OverSupplyTolerancePercent` (15 %) → warning.
   4. **Automatic mode** (no products selected): the default is **NPK 15-15-15 + Urea**, topped up with TSP/MOP only when the crop's P or K needs exceed what the NPK supplies. Internally it tries each set in `AutomaticCandidatePlans` (NPK 15-15-15 + Urea/TSP/MOP, NPK 20-10-10 + straights, straights only, DAP + Urea + MOP) and picks: smallest deficit → lowest cost (only if every candidate is fully priced, e.g. farmer sent prices for all of them) → **first in configured order**. Reorder the list in appsettings.json to change the default.
6. **Rounding.** kg to 1 decimal; bags **rounded up** to the nearest half bag (`BagRoundingIncrement`); `bags50Kg` always counts 50 kg bags, `bags` uses the product's own bag size. Per-plant grams = split kg × 1000 ÷ (plants/ha × ha), shown as bottle caps (≈5 g) or handfuls (≈40 g).
7. **Schedule.** Products carrying P follow the crop's P split; K-only products follow the K split; N-only products fill whatever each split's N share still lacks. So "basal = compound, top-dress = urea" falls out naturally.
8. **Cost** = bags × price per bag (request `customPrices` override stored prices). `costComplete` is false when any product has no price; `totalEstimatedCost` then covers only priced products.
9. **Notes.** Liming advice below pH 5.5, alkaline note above 7.5, soil-type advice, urea handling, manure handling, crop-specific notes, safety, and a closing disclaimer. Warning when mineral fertilizer exceeds `MaxMineralProductKgPerHa` (800 kg/ha).

## Updating the agronomic values

**On a running system (no redeploy)** — use the admin endpoints (see `fertilizer.http`):

* Prices: `PUT /fertilizer/products/{id}` with `pricePerBag` (SLE). PUT replaces every field, so send the full product.
* Crop rates, yields, plant population, notes and schedule: `PUT /fertilizer/crops/{id}`. Each nutrient's schedule percentages must add up to 100.
* New crops/products: `POST /fertilizer/crops`, `POST /fertilizer/products`.

**Seed file** — `Infrastructure/Persistence/Seed/FertilizerReferenceData.cs` is heavily commented for review. It is applied only when the tables are **empty** (first start after the migration), so editing it later only affects new databases. To re-seed a database, clear `FertilizerCrops` / `FertilizerProducts` and restart.

**Multipliers & thresholds** — edit `appsettings.json` → `"FertilizerCalculator"` (or override with environment variables, e.g. `FertilizerCalculator__LimingPhThreshold=5.8`) and restart. The C# defaults in `FertilizerCalculatorOptions` mirror appsettings.json.

## Database migration

The migration has already been created (`Infrastructure/Persistence/Migrations/20260929082151_AddFertilizerCalculator.cs`). It only **creates** the three new tables and their indexes; no existing table is altered.

```bash
# (already done) create the migration
dotnet ef migrations add AddFertilizerCalculator --output-dir Infrastructure/Persistence/Migrations

# review the SQL before applying (from = the migration just before this one)
dotnet ef migrations script AddCropsAndHarvestPredictions AddFertilizerCalculator

# apply (NOT run yet — do this deliberately)
dotnet ef database update
```

> ⚠️ `Program.cs` calls `Database.MigrateAsync()` at start-up, and `appsettings.json` points at the Render
> database. **Simply running the API applies this migration** (and then seeds the reference data). Point
> `ConnectionStrings:DefaultConnection` at a local database first if you don't want that yet.
> (Integration tests use EF InMemory and never touch it.)

## Tests

```bash
dotnet test tests/AgrismartAPI.Tests
```

Unit tests cover unit conversions, yield scaling and cap, each soil fertility level, soil-test mode, limiting-nutrient compound + straight fill, DAP's N, bag rounding, deficit warnings, costs, schedule splitting and validation. Integration tests host the real API (InMemory database) and exercise calculate, errors, reference data, auth, history ownership, admin edits and the OpenAPI examples.

## Assumptions

1. **Separate crop table.** `FertilizerCrops` is separate from the harvest `Crops` table; keys match where both exist (`maize`, `rice-upland`, `rice-lowland`, `cassava`, `sweet-potato`, `groundnut`, `tomato`, `pepper-chili`). Harvest prediction is untouched.
2. **No Farm/Plot entity exists**, so history stores an optional free-text `plotLabel` (filterable, case-insensitive exact match) instead of `FarmId/PlotId`. `UserId` comes from the JWT.
3. **Soil-test units/methods:** total N % (Kjeldahl), Bray-1 P mg/kg, exchangeable K cmol(+)/kg (ammonium acetate). Soil-test mode is inferred when `soil.mode` is omitted and any soil-test value is present. pH is optional in both modes (valid range 3–10).
4. **Soil credit is a class multiplier,** not a kg/ha subtraction — it reuses the same Low/Medium/High factors (configurable separately under `SoilTest:ClassMultipliers`).
5. **Yield-scaling limits** default to 0.5×–2.0× the reference yield.
6. **"Compound" for the limiting-nutrient step means a product with N, P and K** (NPK blends, poultry manure). DAP is labelled Compound but acts as the P filler so its N is credited, as the spec asks.
7. **Seed prices are empty** — prices change quickly; admins set them, or farmers send `customPrices`. Currency is SLE (configurable).
8. **Tree crops** (cocoa, oil palm) are per hectare per year for a mature plantation; `daysAfterPlanting` there means days after the start of the rains.
9. **Per-plant hints** assume ~5 g per level bottle cap and ~40 g per handful (configurable); crops that are broadcast/drilled (rice, groundnut) have no plant population, so no per-plant amounts.
10. **Local (Krio) names** are best-effort and should be checked.
11. **Access:** reference data and `calculate` are anonymous (like harvest prediction); history requires sign-in; users only see their own records (others get 404); admins can see all.
12. **Errors:** the existing `{ "message": "..." }` format is kept; validation errors add an `errors` object keyed by camelCase field path. Malformed JSON / unknown enum strings are rejected by ASP.NET's model binding with its standard 400 `ValidationProblemDetails`, which also has an `errors` object.
13. **Small shared changes** (all backward compatible): `ValidationException` gained an optional `Errors` dictionary; the middleware adds `errors` only when present; `Program.cs` skips `MigrateAsync` for non-relational providers and exposes `public partial class Program` for tests; the csproj now generates XML docs (for OpenAPI) and excludes `tests/**`.
