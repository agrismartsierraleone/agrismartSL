You are working in my existing AgriSmart-UI Flutter app (Riverpod, go_router, `http`, `flutter_secure_storage`). The backend (AgriSmart API, ASP.NET Core) now has a new feature called "Fertilizer Recommendation and Calculator". Build the mobile side of it.

## Goal
Let a farmer work out how much fertilizer to buy and apply for a crop on their farm or plot. They choose a crop, enter the farm/plot size, describe their soil, optionally set a target yield and tick the fertilizers they have. The app then shows:
- a shopping list (kg and 50 kg bags per product, and cost when prices are known)
- when to apply each fertilizer, with an amount per plant (for example "about 1.5 bottle caps per plant")
- warnings and advice notes

Signed-in farmers can save results and view, reopen and delete their history.

## The API contract
`README.fertilizer.mobile.md` (I will put it in the repo root, next to `README.mobile.md`) is the source of truth. Read it fully before you start. It has every endpoint, field, enum value, real request/response JSON, the error format, and design and copy notes. Summary:

- `GET /fertilizer/crops`, `GET /fertilizer/products`, `GET /fertilizer/products/{id}`: no auth
- `POST /fertilizer/calculate`: no auth, stateless
- `POST /fertilizer/recommendations`: auth. Same body as calculate plus an optional `plotLabel`. Returns 201.
- `GET /fertilizer/recommendations?cropId=&plotLabel=&page=&pageSize=`: auth, paginated `{ items, page, pageSize, totalCount, totalPages }`
- `GET /fertilizer/recommendations/{id}` and `DELETE /fertilizer/recommendations/{id}`: auth
- JSON is camelCase and enums are camelCase strings. Responses have no nested nulls: unknown values come back as `""`, `0` or `[]`. Use flags such as `hasPrice`, `costComplete` and `targetYieldCapped` rather than null checks.
- Crop and product ids are strings (`"maize"`, `"npk-15-15-15"`). Never hardcode the lists; always fetch them.
- Validation errors return 400 with `{ "message": "...", "errors": { "area": ["..."], "soil.fertility": ["..."] } }`. The keys are request field paths.

You can try every call live at `/swagger` or `/scalar/v1` on the API.

## Step 0: study the existing app first
Before writing code, look at the app and tell me how you will match its patterns:
- `lib/features/harvest/` is the closest existing feature: crop picker, form, result, history, split into `application/`, `data/`, `domain/` and `presentation/`. Mirror its structure, naming, Riverpod provider style, repository interface + `Http...Repository` implementation, `fromJson` entities, and screen and widget layout.
- `lib/core/network/api_client.dart`, `api_exception.dart` and `token_storage.dart`: how requests, auth headers and errors work.
- `lib/core/router/app_router.dart` and `lib/core/widgets/app_sidebar.dart`: how routes and sidebar entries are added.
- `lib/core/theme` and `lib/core/widgets`: reuse existing components, colours and typography. Do not invent a new visual style.

Then give me a short plan: files to add or change, screens, and providers. Wait for my OK before coding. If anything in the contract or the existing code is unclear, ask instead of guessing.

## What to build
Create a new feature folder `lib/features/fertilizer/` following the harvest layout.

1. **Domain entities** with `fromJson` and, for the request, `toJson`: crop, product, calculation request, calculation result (including soil adjustment, product quantities, schedule stages and schedule products), nutrient amounts (`n`, `p2o5`, `k2o`), recommendation summary, full recommendation, and a paged result. Represent the enums as Dart enums, with mapping to and from the API's camelCase strings.
2. **Repository**: a `FertilizerRepository` interface plus `HttpFertilizerRepository` using the existing `ApiClient`.
3. **Providers**: cached crops and products (`FutureProvider`); a form/controller notifier holding the inputs and calculation status; a history provider with pagination.
4. **Screens and widgets**:
   - **Crop picker.** Show the name, and the Krio `localName` as a subtitle when it is not empty. Show a "Rates per year, mature trees" badge when `isPerennial` is true. Reuse or adapt the harvest crop picker sheet or card if it fits.
   - **Farm size.**
     - Unit switch: acre, hectare, square metre, or plot length × width.
     - Numeric keypad.
     - Show the converted hectares from the result.
   - **Soil.**
     - Simple mode is the default: three large Low/Medium/High cards with plain-language descriptions, optional soil type (sandy/loamy/clay), and an optional pH.
     - "I have a soil test report" mode: total N %, Bray-1 P in mg/kg, exchangeable K in cmol/kg, and pH. Show each unit next to its field.
   - **Target yield** (optional): a slider from the crop's `referenceYieldTPerHa` up to `maxRealisticYieldTPerHa`, labelled with `yieldBasis`.
   - **Fertilizers** (optional):
     - "Choose for me" is the default and sends an empty or omitted `fertilizerProductIds`. The API then uses NPK 15-15-15 + Urea, plus TSP or MOP only if needed.
     - Otherwise, multi-select product chips grouped or badged by `type` (compound, straight, organic).
     - Optional price per bag (SLE) for each selected product. Send it as `customPrices`, prefilled from `pricePerBag` when `hasPrice` is true.
   - **Result screen**, in this order:
     1. A warnings banner, shown only when `warnings` is not empty. Use an icon plus text, not colour alone.
     2. A shopping list: one card per product showing `bags50Kg` in large text (render .5 as "½"), `quantityKg` underneath, and `estimatedCost` only when `hasPrice` is true (otherwise an "Add price" action that recalculates).
     3. The total cost, labelled "partial" when `costComplete` is false.
     4. An application timeline from `schedule`: `stage`, `timing`, "Day `daysAfterPlanting`", and each product's `quantityKg` with `perPlantHint` shown prominently when it is not empty.
     5. An expandable "Good practice" list from `notes`. The last note is always the disclaimer; keep it visible.
     6. A collapsible "Details" section: required vs supplied nutrients (a simple 3-bar comparison), `nutrientBalanceKg`, the soil classes, `targetYieldTPerHa` with a "capped" chip, and "Chosen for you" when `selectionMode` is `automatic`.
     7. An empty state when `products` is `[]`: no fertilizer needed.
   - **Save**: needs sign-in, with an optional plot name (`plotLabel`, max 100 characters). If the user is not signed in, follow the app's existing login redirect.
   - **History**: a paginated list (crop, plot label, area, total cost, date) with optional crop and plot filters. Tapping an item reopens the saved `result` in the same result widget. Add "Recalculate" (prefills the form from `inputs`) and "Delete" (with a confirmation).
5. **Navigation**: add the routes in `app_router.dart` and a sidebar entry "Fertilizer calculator", plus "Fertilizer history" if that matches how harvest is listed. Put them next to the harvest entries.
6. **Errors**:
   - Extend `ApiException` with an optional `Map<String, List<String>> errors`, parsed from the response body in `ApiClient`. This must be backward compatible: `message` stays exactly as it is, and existing callers must not change.
   - Map the `errors` keys (`area`, `lengthM`, `widthM`, `soil.fertility`, `soil.phosphorusBray1MgPerKg`, `targetYieldTPerHa`, `customPrices[0].pricePerBag` and so on) to inline field errors, with `message` as a snackbar fallback.
   - A 400 for an unknown enum or malformed JSON uses ASP.NET's `errors` with keys like `$.areaUnit`. Just show `message` or `title` for those.
   - Mirror the server's basic checks on the client: area > 0, both dimensions in plot mode, fertility required in simple mode, all three soil-test values ≥ 0, pH between 3 and 10.
7. **Behaviour**:
   - Recalculating is cheap and needs no login. Debounce live recalculation by about 500 ms, or use an explicit "Calculate" button if that suits the existing UX better.
   - Cache crops and products.
   - Show loading, error and retry states the same way the harvest screens do.

## Design requirements
- Plain language and short sentences. Many users have low literacy, so pair numbers with icons (a bag icon × 3½, a bottle-cap icon).
- Always show units (kg, bags, SLE, per plant). Don't show raw nutrient maths on the main result view.
- Touch targets of at least 48 dp, support large text, and don't use colour alone to signal anything.
- Use the existing theme and components, so it should look like the rest of the app.

## Quality
- Unit tests:
  - JSON parsing of the result and history models, using the example JSON from `README.fertilizer.mobile.md`
  - enum string mapping
  - request `toJson` for simple, soil-test, plot-dimension and custom-price inputs
  - `ApiException` `errors` parsing, including that the old `{ "message" }` shape still works
- Widget tests for the result screen: the warnings banner, the price-missing state, the per-plant hint, and the empty state.
- `flutter analyze` must be clean, and existing tests must still pass.
- No breaking changes to existing features, especially harvest prediction, auth and the market.

## Deliverables
1. The Step 0 findings and plan. Wait for my OK.
2. All new and changed files.
3. A short summary of the screens and how to reach them.
4. Any assumptions you made, and anything in the API contract that was awkward for the UI, so I can adjust the backend.
5. Do not commit or push to GitHub.
6. Use the README.fertilizer.md and README.fetilizer.mobile.md as an additional guide
