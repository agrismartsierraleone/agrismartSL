# Fertilizer Calculator — Mobile App Integration & Design Guide (Flutter)

Audience: the designer and Flutter developer building the **Fertilizer Calculator** screens in AgriSmart-UI.
Backend details and formulas: [README.fertilizer.md](README.fertilizer.md). General app conventions (base URLs, auth, tokens): [README.mobile.md](README.mobile.md).

Base URLs (local dev): `http://localhost:5150` · `https://localhost:7004`. Try every call live at `/swagger` or `/scalar/v1` — each fertilizer endpoint has an example request and response.

## What the feature does

The farmer picks a **crop**, enters their **farm or plot size**, describes their **soil**, optionally sets a **target yield** and ticks the **fertilizers they have**. The API answers:

* how many **kg** and **50 kg bags** of each fertilizer to buy,
* **when** to apply each one (basal / top-dressing, days after planting), with a **per-plant amount** ("about 1.5 bottle caps per plant"),
* the **cost** in SLE when prices are known,
* **warnings** (e.g. no potassium source) and **advice notes** (e.g. apply lime).

## JSON conventions

* camelCase keys; enums are **camelCase strings** (`"hectare"`, `"soilTest"`, `"compound"`).
* Responses have **no nulls in nested objects or lists**: unknown strings are `""`, unknown numbers `0`, empty lists `[]`. Check flags such as `hasPrice` / `costComplete` instead of null checks.
* Nutrient objects are always `{ "n": 0.0, "p2o5": 0.0, "k2o": 0.0 }` (kg). Show them as **N**, **P₂O₅**, **K₂O**.
* Crop and product `id`s are stable strings (`"maize"`, `"npk-15-15-15"`), safe to keep in local state.

## Suggested screens & flow

```
[Crop picker] → [Farm size] → [Soil] → [Fertilizers (optional)] → [Result] → (Save) → [History]
```

A single scrolling form with sections also works. `POST /fertilizer/calculate` is cheap and needs no login, so you can recalculate live as inputs change (debounce ~500 ms).

### 1. Crop picker — `GET /fertilizer/crops` (no auth)

```json
[
  {
    "id": "maize",
    "name": "Maize",
    "localName": "Kon",
    "nutrientRequirementKgPerHa": { "n": 90, "p2o5": 45, "k2o": 45 },
    "referenceYieldTPerHa": 3.5,
    "maxRealisticYieldTPerHa": 7,
    "yieldBasis": "dry grain",
    "plantsPerHectare": 53333,
    "isPerennial": false,
    "notes": "",
    "schedule": [
      { "stage": "Basal (at planting)", "daysAfterPlanting": 0, "timing": "At planting or within 1 week, in a band beside the row", "nitrogenPercent": 40, "phosphatePercent": 100, "potashPercent": 100 },
      { "stage": "Top-dressing", "daysAfterPlanting": 35, "timing": "4-6 weeks after planting, before tasselling", "nitrogenPercent": 60, "phosphatePercent": 0, "potashPercent": 0 }
    ]
  }
]
```

* 11 crops: upland rice, lowland/IVS rice, maize, cassava, sweet potato, groundnut, pepper, tomato, okra, cocoa, oil palm.
* Show `name` large and `localName` (Krio) as a subtitle when it isn't `""`. Crop icons/illustrations are recommended: many users read little.
* `isPerennial: true` (cocoa, oil palm): show a badge like "Rates per year, mature trees".
* Cache this list; it changes rarely.

### 2. Farm size

| Field | Type | Notes |
|---|---|---|
| `areaUnit` | `"hectare"` \| `"acre"` \| `"squareMeter"` \| `"plotDimensions"` | Segmented control. Default **acre** is often most familiar to farmers. |
| `area` | number > 0 | For hectare / acre / squareMeter. |
| `lengthM`, `widthM` | numbers > 0 | Only for `plotDimensions` ("My plot is 20 m × 15 m"). |

Use a numeric keypad. The result echoes back `areaHectares`, so you can show "= 0.40 ha".

### 3. Soil — two modes (`soil` object, required)

**Simple mode** (default; most farmers):

| Field | Values | UI |
|---|---|---|
| `mode` | `"simple"` | (may be omitted) |
| `fertility` | `"low"` \| `"medium"` \| `"high"` | **Required.** Three big cards with plain language, e.g. *Low: crops were poor last season, pale leaves* · *Medium: average* · *High: new land / dark soil / after fallow*. |
| `soilType` | `"sandy"` \| `"loamy"` \| `"clay"` | Optional. Pictures of soil texture help. |
| `ph` | 3.0 – 10.0 | Optional. "I have a pH reading" toggle. |

**Soil-test mode** (behind "I have a soil test report"):

| Field | Unit shown on lab report | Required |
|---|---|---|
| `mode` | `"soilTest"` | yes (or it's inferred when any value below is sent) |
| `nitrogenTotalPercent` | Total N, % | yes, ≥ 0 |
| `phosphorusBray1MgPerKg` | Available P (Bray-1), mg/kg or ppm | yes, ≥ 0 |
| `potassiumCmolPerKg` | Exchangeable K, cmol/kg or meq/100 g | yes, ≥ 0 |
| `ph`, `soilType` | as above | optional |

Show the units next to each input, since labs report in these units.

### 4. Target yield (optional)

`targetYieldTPerHa`: a number. Default hint = crop's `referenceYieldTPerHa`; a slider up to `maxRealisticYieldTPerHa` works well. Label it with the crop's `yieldBasis` ("t/ha of dry grain"). Values above the max are accepted but capped, with a warning.

### 5. Fertilizers the farmer has (optional) — `GET /fertilizer/products`

```json
{
  "id": "npk-15-15-15", "name": "NPK 15-15-15", "type": "compound",
  "nitrogenPercent": 15, "phosphatePercent": 15, "potashPercent": 15,
  "sulphurPercent": 0, "zincPercent": 0, "bagSizeKg": 50,
  "pricePerBag": 0, "hasPrice": false, "currency": "SLE",
  "notes": "Balanced compound — the most widely available fertilizer in Sierra Leone."
}
```

* Multi-select chips/cards → `fertilizerProductIds: ["npk-15-15-15", "urea"]`.
* **Nothing selected = "Choose for me"**: the API uses the standard recommendation, **NPK 15-15-15 + Urea** (plus TSP or MOP only if the crop needs more P or K). If the farmer has entered prices for all products, it picks the cheapest combination instead. Make "Choose for me" the default and label it clearly.
* `type`: `compound`, `straight` or `organic`. Group or badge by type (organic = green leaf icon).
* Optional price entry per selected product → `customPrices: [{ "productId": "urea", "pricePerBag": 900 }]` (SLE per bag). Prefill from `pricePerBag` when `hasPrice` is true.

### 6. Calculate — `POST /fertilizer/calculate` (no auth)

Request:

```json
{
  "cropId": "maize",
  "area": 0.5,
  "areaUnit": "hectare",
  "soil": { "mode": "simple", "fertility": "medium", "soilType": "loamy", "ph": 5.2 },
  "targetYieldTPerHa": 4.0,
  "fertilizerProductIds": ["npk-15-15-15", "urea"],
  "customPrices": [
    { "productId": "npk-15-15-15", "pricePerBag": 950 },
    { "productId": "urea", "pricePerBag": 900 }
  ]
}
```

Response `200` (real output):

```json
{
  "cropId": "maize",
  "crop": "Maize",
  "areaHectares": 0.5,
  "targetYieldTPerHa": 4,
  "targetYieldCapped": false,
  "yieldBasis": "dry grain",
  "soilAdjustment": {
    "mode": "simple", "nitrogenClass": "medium", "phosphorusClass": "medium", "potassiumClass": "medium",
    "nitrogenFactor": 1, "phosphateFactor": 1, "potashFactor": 1
  },
  "nutrientRequirementPerHaKg": { "n": 102.9, "p2o5": 51.4, "k2o": 51.4 },
  "nutrientRequirementKg": { "n": 51.4, "p2o5": 25.7, "k2o": 25.7 },
  "selectionMode": "farmerSelected",
  "products": [
    {
      "productId": "npk-15-15-15", "name": "NPK 15-15-15", "type": "compound",
      "quantityKg": 171.4, "bagSizeKg": 50, "bags": 3.5, "bags50Kg": 3.5,
      "pricePerBag": 950, "hasPrice": true, "estimatedCost": 3325.0,
      "nutrientsSupplied": { "n": 25.7, "p2o5": 25.7, "k2o": 25.7 }
    },
    {
      "productId": "urea", "name": "Urea (46-0-0)", "type": "straight",
      "quantityKg": 55.9, "bagSizeKg": 50, "bags": 1.5, "bags50Kg": 1.5,
      "pricePerBag": 900, "hasPrice": true, "estimatedCost": 1350.0,
      "nutrientsSupplied": { "n": 25.7, "p2o5": 0, "k2o": 0 }
    }
  ],
  "nutrientsSupplied": { "n": 51.4, "p2o5": 25.7, "k2o": 25.7 },
  "nutrientBalanceKg": { "n": 0, "p2o5": 0, "k2o": 0 },
  "schedule": [
    {
      "stage": "Basal (at planting)", "daysAfterPlanting": 0,
      "timing": "At planting or within 1 week, in a band beside the row",
      "nutrients": { "n": 25.7, "p2o5": 25.7, "k2o": 25.7 },
      "products": [
        { "productId": "npk-15-15-15", "name": "NPK 15-15-15", "quantityKg": 171.4, "gramsPerPlant": 6.4,
          "perPlantHint": "about 1.5 level bottle caps per plant (1 cap ≈ 5 g)" }
      ]
    },
    {
      "stage": "Top-dressing", "daysAfterPlanting": 35,
      "timing": "4-6 weeks after planting, before tasselling",
      "nutrients": { "n": 25.7, "p2o5": 0, "k2o": 0 },
      "products": [
        { "productId": "urea", "name": "Urea (46-0-0)", "quantityKg": 55.9, "gramsPerPlant": 2.1,
          "perPlantHint": "about 0.5 level bottle cap per plant (1 cap ≈ 5 g)" }
      ]
    }
  ],
  "plantCount": 26666,
  "totalEstimatedCost": 4675.0,
  "costComplete": true,
  "currency": "SLE",
  "warnings": [],
  "notes": [
    "Soil pH 5.2 is acidic (below 5.5). Apply agricultural lime or dolomite — typically 1–2 t/ha, ...",
    "Cover Urea / Ammonium Sulphate with soil after applying, ...",
    "Place fertilizer 5–10 cm away from seeds and stems ...",
    "These rates are general guidelines based on regional averages and must be validated locally. ..."
  ]
}
```

## Result screen — what to show, in priority order

1. **Warnings banner** (`warnings[]`, amber/red). Show them *above* the results when the list isn't empty. They mean the plan is incomplete or risky, e.g. *"No potassium source selected — K2O deficit of 50 kg. Add MOP (Muriate of Potash)."*
2. **Shopping list** (`products[]`): one card per product.
   * Headline: **`bags50Kg` bags** (e.g. "3½ bags"), with `quantityKg` kg underneath. Render .5 as "½".
   * Show `estimatedCost` only when `hasPrice` is true. Otherwise show "Add price" → opens price entry → recalculate with `customPrices`.
   * `bags` equals `bags50Kg` unless a product has a non-50 kg bag (`bagSizeKg`). Then show "`bags` × `bagSizeKg` kg bags".
3. **Total cost**: `totalEstimatedCost` + `currency`. If `costComplete` is false, label it "partial: some prices missing" (or hide it when it is 0).
4. **When to apply** (`schedule[]`): a timeline or stepper, one step per stage.
   * Title `stage`, subtitle `timing`, badge "Day `daysAfterPlanting`". For perennials, days count from the start of the rains; `timing` says so.
   * Under each stage, list its `products[]` with `quantityKg`. When `perPlantHint` isn't `""`, show it prominently. Farmers measure per plant, not per field.
   * Optional: offer "Add reminder" using the planting date the user enters locally (the API doesn't store planting dates).
5. **Advice** (`notes[]`): an expandable "Good practice" list. The **last note is always the disclaimer**; keep it visible (small print is fine).
6. **Details** (collapsible, for extension officers): `nutrientRequirementKg` vs `nutrientsSupplied` (a small 3-bar chart works), `nutrientBalanceKg` (+ = over-supply, − = shortfall), `soilAdjustment`, `targetYieldTPerHa` (+ "capped" chip if `targetYieldCapped`), and `selectionMode` (`"automatic"` → "Chosen for you").

Empty result: if `products` is `[]`, the first note says no fertilizer is needed. Show a friendly empty state.

## Saving & history (login required)

Send `Authorization: Bearer <token>` (see README.mobile.md; tokens last 10 minutes, and on `401` go to login).

| Action | Call | Notes |
|---|---|---|
| Save | `POST /fertilizer/recommendations` | **Same body as calculate**, plus optional `"plotLabel": "Back field by the stream"` (max 100 chars). Returns `201` with the saved record. |
| History | `GET /fertilizer/recommendations?page=1&pageSize=20` | Optional filters: `cropId`, `plotLabel` (exact, case-insensitive). Newest first. |
| Open one | `GET /fertilizer/recommendations/{id}` | Full `inputs` + `result` exactly as calculated then. |
| Delete | `DELETE /fertilizer/recommendations/{id}` | `204`. Confirm with the user first. |

History page response:

```json
{
  "items": [
    { "id": "3f2b8c1e-4d5a-4b7e-9c61-2a8e7d4f1b90", "cropId": "maize", "cropName": "Maize",
      "plotLabel": "Back field by the stream", "areaHectares": 0.5,
      "totalEstimatedCost": 4675.0, "currency": "SLE", "createdAt": "2026-09-29T10:30:00+00:00" }
  ],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```

Saved record (`GET /fertilizer/recommendations/{id}` and the `201` from save):

```json
{
  "id": "3f2b8c1e-...", "userId": "9a1c6e52-...", "cropId": "maize", "cropName": "Maize",
  "plotLabel": "Back field by the stream", "areaHectares": 0.5, "createdAt": "2026-09-29T10:30:00+00:00",
  "inputs": { /* the request you sent */ },
  "result": { /* the same shape as the calculate response */ }
}
```

Reuse the Result screen widget for `result`. "Recalculate" can prefill the form from `inputs`.

## Errors

| Status | When | Body |
|---|---|---|
| `400` | Invalid input | `{ "message": "Validation failed: ...", "errors": { "area": ["area must be greater than 0."], "soil.fertility": ["..."] } }` |
| `400` | Unknown enum string (e.g. `"areaUnit": "furlong"`) or malformed JSON | ASP.NET `ValidationProblemDetails`, which also has an `errors` object, keyed like `"$.areaUnit"` |
| `401` | Save/history without a valid token | empty |
| `403` | Non-admin calling admin endpoints | empty |
| `404` | Unknown crop/product id on GET, or someone else's recommendation | `{ "message": "..." }` |

`errors` keys are **field paths matching the request JSON**: `cropId`, `area`, `areaUnit`, `lengthM`, `widthM`, `soil`, `soil.fertility`, `soil.ph`, `soil.nitrogenTotalPercent`, `soil.phosphorusBray1MgPerKg`, `soil.potassiumCmolPerKg`, `targetYieldTPerHa`, `fertilizerProductIds[0]`, `customPrices[0].pricePerBag`, `plotLabel`. Map them to inline field errors. Use `message` as a fallback snackbar.

Client-side checks worth mirroring (the server checks them anyway): area > 0; both dimensions for plot mode; fertility required in simple mode; all three soil-test values ≥ 0 in soil-test mode; pH 3–10.

## Design & copy notes

* **Language:** plain words, short sentences. Many users have low literacy, so pair numbers with icons (bag icon × 3½, bottle-cap icon).
* **Units:** always show the unit ("kg", "bags", "SLE", "per plant"). Never show bare nutrient math on the main screen.
* **Trust:** keep the disclaimer and the "ask your extension officer" advice visible. The values are guidelines.
* **Offline:** cache `/fertilizer/crops` and `/fertilizer/products`. Calculation needs the network; queue "save" if offline.
* **Accessibility:** touch targets ≥ 48 dp, don't rely on colour alone for warnings (use an icon + text), and support large text.
* **Admin screens** (web admin app, not the farmer app): `PUT /fertilizer/products/{id}` to set prices and `PUT /fertilizer/crops/{id}` for agronomic values. See `fertilizer.http`.
