# My Farm — Mobile App Integration & Design Guide (Flutter)

Audience: the designer and Flutter developer building the **My Farm** screen (bottom-nav tab "My Farm") in AgriSmart-UI.
General app conventions (base URLs, login, tokens): [README.mobile.md](README.mobile.md). Creating/editing the farm itself (`/farms`) is documented in [README.assistant.mobile.md](README.assistant.mobile.md), Part 1.

Base URLs (local dev): `http://localhost:5150` · `https://localhost:7004`. Try every call live at `/swagger` or `/scalar/v1` (tag **My farm**).

Design reference: `wwwroot/uploads/MyFarm.jpeg` (the "Green Valley Farm" screen).

## What the feature does

My Farm is where a farmer manages one farm: the crops growing on it, what work is due, how healthy the plants are, where the plots are and how the season is going.

| On the design | What the API gives you |
|---|---|
| Header: **Green Valley Farm ⌄ · 4.5 ha · Port Loko · ✎** | Farm name, size, district; the ⌄ switches farm (`GET /farms`), ✎ edits it (`PUT /farms/{id}`) |
| Tabs **Crops · Map · Activities · Analytics** | One endpoint per tab (below) |
| **My Crops** cards + **Add Crop** | Crop name, plot size, status badge, variety, planted date, expected harvest, progress bar %, growth stage, photo |
| **Crop Calendar (Rice – 3.0 ha)** strip + **View Full Calendar** | Six stages (Land Prep → Harvesting) with date, status (✓ done / current / upcoming / overdue) and the stage to highlight |
| **Upcoming Activities** + **See All** | Tasks with icon type, crop + plot ("Rice – Plot 1"), due date and a "Tomorrow" badge; generated automatically from the crop calendar when a crop is added |
| **Crop Health** card, **Scan Plant**, **View History** | Last scan photo, date, result ("No major disease detected"), confidence % |

## Before you start

* Every endpoint needs the login token: `Authorization: Bearer <token>`. On `401`, send the user to login and retry.
* A farmer only sees **their own** farms. Another farmer's farm/crop/activity id returns `404` (show "not found", never "forbidden").
* JSON is camelCase; enums are camelCase strings (`"readyToHarvest"`, `"pestControl"`).
* Dates are `yyyy-MM-dd` strings (no time). Timestamps (`createdAt`, `scannedAt`) are ISO-8601 with offset.
* Optional fields can be `null` — handle them (e.g. `variety`, `expectedHarvestDate`, `imageUrl`, `growthStage`).
* Image URLs (`imageUrl`) are root-relative (`/uploads/farm-crops/x.jpg`). Prefix with the API base URL. `null` = show your bundled default picture for that `cropId`.
* Money is in Leones (`"currency": "SLE"`).
* "Today" is the server's UTC date, which is Sierra Leone time (GMT, no daylight saving).

---

## Screen → endpoint map

```
[My Farm tab]
   │  GET /farms                         → farm switcher (⌄). Pick the first farm by default; remember the last one.
   │  (no farms?) → "Add your farm" form → POST /farms
   ▼
[Crops tab]   GET /farms/{farmId}/overview          ← one call renders the whole design
   ├─ Add Crop            → GET /farms/options (crop list) → POST /farms/{farmId}/crops
   ├─ crop card ›         → GET /farms/{farmId}/crops/{cropId}  (details / edit / harvest / photo)
   ├─ View Full Calendar  → GET /farms/{farmId}/crops/{cropId}/calendar
   ├─ See All             → Activities tab
   ├─ activity ›          → complete / skip / edit
   ├─ Scan Plant          → POST /farms/{farmId}/health/scans  (camera / gallery)
   └─ View History        → GET /farms/{farmId}/health
[Map tab]        GET /farms/{farmId}/map        (PUT to draw the farm outline)
[Activities tab] GET /farms/{farmId}/activities (filters + paging)
[Analytics tab]  GET /farms/{farmId}/analytics
```

## Endpoints

| Method | Route | What it does | Success |
|---|---|---|---|
| `GET` | `/farms/options` | Lists for forms: districts, crops, activity types, area units | `200` |
| `GET` | `/farms/{farmId}/overview?calendarCropId=&activitiesLimit=3` | **Crops tab** in one call | `200` |
| `GET` | `/farms/{farmId}/crops?status=` | Crop cards (filter: `planned`, `growing`, `readyToHarvest`, `harvested`, `failed`) | `200` list |
| `GET` | `/farms/{farmId}/crops/{cropId}` | One crop card | `200` |
| `POST` | `/farms/{farmId}/crops` | **Add Crop** (also plans its tasks) | `201` crop |
| `PUT` | `/farms/{farmId}/crops/{cropId}` | Edit crop (all fields replaced) | `200` crop |
| `DELETE` | `/farms/{farmId}/crops/{cropId}` | Delete crop + its open tasks | `204` |
| `POST` | `/farms/{farmId}/crops/{cropId}/image` | Upload crop photo (multipart `image`) | `200` crop |
| `POST` | `/farms/{farmId}/crops/{cropId}/harvest` | Mark harvested, optional yield | `200` crop |
| `PUT` | `/farms/{farmId}/crops/{cropId}/status` | `{ "status": "failed" }` or `"active"` (undo) | `200` crop |
| `GET` | `/farms/{farmId}/crops/{cropId}/calendar` | **Full calendar**: stages + all tasks for the crop | `200` |
| `GET` | `/farms/{farmId}/activities?status=&farmCropId=&type=&from=&to=&page=1&pageSize=20` | **Activities tab** (paged) | `200` page |
| `POST` | `/farms/{farmId}/activities` | Add a task | `201` activity |
| `PUT` | `/farms/{farmId}/activities/{activityId}` | Edit a task | `200` activity |
| `POST` | `/farms/{farmId}/activities/{activityId}/complete` | Mark done (body optional) | `200` activity |
| `POST` | `/farms/{farmId}/activities/{activityId}/skip` | Not needed | `200` activity |
| `POST` | `/farms/{farmId}/activities/{activityId}/reopen` | Back to pending | `200` activity |
| `DELETE` | `/farms/{farmId}/activities/{activityId}` | Delete a task | `204` |
| `GET` | `/farms/{farmId}/health` | **Crop Health** + **View History** | `200` |
| `POST` | `/farms/{farmId}/health/scans` | **Scan Plant** (multipart `image`, optional `farmCropId`) | `200` scan |
| `GET` | `/farms/{farmId}/map` | **Map tab** | `200` |
| `PUT` | `/farms/{farmId}/map` | Set farm pin and outline | `200` map |
| `GET` | `/farms/{farmId}/analytics` | **Analytics tab** | `200` |

Existing farm endpoints still apply: `GET /farms`, `POST /farms`, `PUT /farms/{id}`, `DELETE /farms/{id}`.
> ⚠️ When editing the farm header with `PUT /farms/{id}`, **omit `crops`** (or send `null`). Sending a `crops` list replaces every crop and drops their details (variety, photo, plot names, tasks). Manage crops with the `/farms/{farmId}/crops` endpoints instead.

---

## 1. Crops tab — `GET /farms/{farmId}/overview`

```json
{
  "farm": {
    "id": "6a0f…", "name": "Green Valley Farm", "district": "Port Loko",
    "areaHectares": 4.5, "latitude": 8.77, "longitude": -12.78,
    "activeCropCount": 2, "cultivatedHectares": 4.5
  },
  "crops": [
    {
      "id": "c1…", "farmId": "6a0f…",
      "cropId": "rice-lowland", "cropName": "Rice (lowland)", "localName": "Res",
      "plotName": "Plot 1", "variety": "NERICA 14", "plotSizeHectares": 3.0,
      "plantingDate": "2025-06-12", "expectedHarvestDate": "2025-10-18", "expectedHarvestIsEstimate": true,
      "daysToHarvest": 96, "status": "growing", "progressPercent": 45, "growthStage": "Tillering",
      "harvestedOn": null, "imageUrl": null, "location": null, "boundary": [], "notes": null,
      "pendingActivityCount": 6, "createdAt": "2025-06-10T09:12:44+00:00"
    }
  ],
  "calendar": {
    "farmCropId": "c1…", "cropId": "rice-lowland", "cropName": "Rice (lowland)", "plotName": "Plot 1",
    "plotSizeHectares": 3.0, "currentStageKey": "fertilizing",
    "stages": [
      { "key": "landPreparation", "name": "Land Prep",    "activityType": "landPreparation", "startDate": "2025-05-22", "endDate": "2025-06-11", "keyDate": "2025-05-22", "datePrecision": "month", "status": "completed",  "activityCount": 0, "completedActivityCount": 0 },
      { "key": "planting",        "name": "Planting",     "activityType": "planting",        "startDate": "2025-06-12", "endDate": "2025-06-15", "keyDate": "2025-06-12", "datePrecision": "day",   "status": "completed",  "activityCount": 0, "completedActivityCount": 0 },
      { "key": "fertilizing",     "name": "Fertilizing",  "activityType": "fertilizing",     "startDate": "2025-06-12", "endDate": "2025-08-03", "keyDate": "2025-07-03", "datePrecision": "month", "status": "inProgress", "activityCount": 1, "completedActivityCount": 0 },
      { "key": "weeding",         "name": "Weeding",      "activityType": "weeding",         "startDate": "2025-06-26", "endDate": "2025-08-11", "keyDate": "2025-07-03", "datePrecision": "month", "status": "inProgress", "activityCount": 1, "completedActivityCount": 0 },
      { "key": "pestControl",     "name": "Pest Control", "activityType": "pestControl",     "startDate": "2025-07-03", "endDate": "2025-09-22", "keyDate": "2025-07-27", "datePrecision": "month", "status": "inProgress", "activityCount": 1, "completedActivityCount": 0 },
      { "key": "harvesting",      "name": "Harvesting",   "activityType": "harvesting",      "startDate": "2025-10-18", "endDate": "2025-11-01", "keyDate": "2025-10-18", "datePrecision": "day",   "status": "upcoming",   "activityCount": 1, "completedActivityCount": 0 }
    ],
    "activities": []
  },
  "upcomingActivities": [
    {
      "id": "a1…", "farmId": "6a0f…", "farmCropId": "c1…", "cropId": "rice-lowland", "cropName": "Rice (lowland)", "plotName": "Plot 1",
      "type": "fertilizing", "title": "Fertilizer application (2nd dose)", "notes": "Top-dressing 1 (tillering): About 3 weeks after transplanting.",
      "dueDate": "2025-07-15", "dueInDays": 1, "isOverdue": false, "status": "pending",
      "completedOn": null, "cost": null, "source": "calendar", "createdAt": "2025-06-10T09:12:44+00:00"
    }
  ],
  "pendingActivityCount": 7,
  "overdueActivityCount": 0,
  "cropHealth": {
    "scanId": "s1…", "imageUrl": "/uploads/scans/x.jpg", "scannedAt": "2025-07-10T08:01:00+00:00",
    "farmCropId": "c1…", "cropName": "Rice (lowland)", "plantName": "Rice", "diseaseName": null,
    "severity": "healthy", "isHealthy": true, "confidencePercent": 92,
    "headline": "No major disease detected", "summary": "…", "treatmentSteps": []
  },
  "cropHealthIsFromThisFarm": true
}
```

How to render each part:

**Header** — `"{farm.areaHectares} ha · {farm.district}"`. Hide either part if `null`.

**Crop card**
* Title: `cropName` (you may shorten `"Rice (lowland)"` → `"Rice"` by cutting at `" ("`). Sub-title: `"{plotSizeHectares} ha"`.
* Badge from `status`:

  | `status` | Badge text | Colour hint |
  |---|---|---|
  | `planned` | Planned | grey |
  | `growing` | Growing | light green bg, green text (as designed) |
  | `readyToHarvest` | Ready to harvest | amber |
  | `harvested` | Harvested | blue |
  | `failed` | Failed | red |

* Rows: Variety (`variety`, hide row if null) · Planted (`plantingDate`, format `d MMM yyyy`) · Expected Harvest (`expectedHarvestDate`; if `expectedHarvestIsEstimate` you may prefix "~").
* Progress bar = `progressPercent / 100`, label `"{progressPercent}%"`.
* "Growth Stage: **{growthStage}**" — hide when `null` (planned crops).
* Picture: `imageUrl` if set, else your bundled image for `cropId` (rice, cassava, maize, …). Ship one default image per crop id in `GET /farms/options`.

**Crop Calendar strip** — title `"Crop Calendar ({cropName short} - {plotSizeHectares} ha)"`.
* One circle per `stages[i]`, label `name`, date under it from `keyDate`: `datePrecision == "day"` → `"12 Jun"`, `"month"` → `"Jul"`. `keyDate == null` (no planting date yet) → show "—".
* Icon per `key` (suggested Material icons): `landPreparation` → `Icons.landscape`, `planting` → `Icons.spa`, `fertilizing` → `Icons.science`/fertilizer bag, `weeding` → `Icons.grass`, `pestControl` → `Icons.bug_report`, `harvesting` → `Icons.agriculture`.
* State: `completed` → green check badge on the circle and a green connector line; the stage whose `key == currentStageKey` → highlighted (blue ring, as in the design); `overdue` → red dot; others → grey.
* To show another crop's calendar, call overview again with `?calendarCropId={crop.id}` (or use the calendar endpoint).

**Upcoming Activities** — show `upcomingActivities` (3 by default; change with `activitiesLimit`).
* Line 1: `title`. Line 2: `"{cropName short} - {plotName}"` (whole-farm task: both null → show the farm name).
* Right side: `dueDate` (`d MMM yyyy`) and a badge from `dueInDays`: `0` → **Today**, `1` → **Tomorrow** (the orange pill in the design), `isOverdue` → **Overdue** (red). Otherwise no badge.
* Icon tile colour per `type`:

  | `type` | Icon | Tile colour |
  |---|---|---|
  | `fertilizing` | flask / fertilizer | amber `#F9A825` |
  | `weeding` | leaf / grass | green `#2E7D32` |
  | `pestControl` | bug | blue `#1565C0` |
  | `planting` | seedling | light green |
  | `landPreparation` | tractor / shovel | brown |
  | `harvesting` | grain | gold |
  | `irrigation` | water drop | cyan |
  | `scouting` | eye / search | purple |
  | `other` | checklist | grey |

* Tap → activity sheet with **Mark as done**, **Skip**, **Edit**, **Delete**. Swipe-to-complete is a nice shortcut.

**Crop Health card** — `cropHealth` may be `null` (never scanned): show an empty state "Scan a plant to check its health" with the **Scan Plant** button.
* "Last Scan: {scannedAt as d MMM yyyy}", icon ✓ green if `isHealthy` else ⚠ amber/red by `severity` (`low` amber, `moderate`/`high` red), text = `headline`, "Confidence: **{confidencePercent}%**", photo = `imageUrl`.
* If `cropHealthIsFromThisFarm` is `false`, the scan was taken elsewhere in the app: add a small caption "From your latest scan".

## 2. Add / edit a crop — `POST /farms/{farmId}/crops`, `PUT …/crops/{cropId}`

Get the crop dropdown from `GET /farms/options` → `crops[]` (`id`, `name`, `localName`, `typicalDaysToMaturity`).

```json
{
  "cropId": "rice-lowland",
  "plotName": "Plot 1",
  "variety": "NERICA 14",
  "plantingDate": "2025-06-12",
  "expectedHarvestDate": null,
  "plotSize": 3,
  "areaUnit": "hectare",
  "latitude": null,
  "longitude": null,
  "boundary": null,
  "notes": null,
  "generateActivities": true
}
```

* Only `cropId` is required. Leave `plantingDate` empty for a planned crop.
* `plotName` defaults to "Plot 1", "Plot 2", … on add.
* `expectedHarvestDate` empty → estimated from the crop's typical days to maturity (`expectedHarvestIsEstimate: true`). Let the farmer override it.
* `plotSize` + `areaUnit` (`hectare` default, `acre`, `squareMeter`) — returned as `plotSizeHectares`.
* `generateActivities: true` (default) adds the crop calendar's **upcoming** tasks: land preparation, planting, each fertilizer dose (from the fertilizer calculator's schedule), two weedings, two pest checks (crop-specific, e.g. "Monitor for stem borers" for rice, "fall armyworm" for maize) and harvest. Tasks dated before today are not created.
* **Edit is a full replace**: send all fields, prefilled from the card. If the crop, planting date or harvest date changes, open calendar-generated tasks are re-planned; tasks the farmer added or finished are kept. Send `generateActivities: false` to keep tasks as they are.
* Response: the crop card (`201` on add).

**Photo**: `POST /farms/{farmId}/crops/{cropId}/image`, multipart field `image` (JPEG/PNG ≤ 10 MB). Returns the card with `imageUrl`.

**Harvest**: `POST …/crops/{cropId}/harvest` with `{ "harvestedOn": "2025-10-20", "quantity": 1200, "unit": "kg", "notes": null }` (all optional; `unit` required with `quantity`). Status becomes `harvested`, open harvest tasks are completed, other open calendar tasks are skipped, and the yield appears in Analytics (and is used by the AI Assistant).

**Failed / undo**: `PUT …/crops/{cropId}/status` with `{ "status": "failed" }` or `{ "status": "active" }`.

**Delete**: confirm first ("Delete Rice – Plot 1? Its open tasks will be removed."). Completed tasks stay in history.

## 3. Full calendar — `GET /farms/{farmId}/crops/{cropId}/calendar`

Same `stages` and `currentStageKey` as the overview, plus `activities`: every task for this crop (any status) by due date. Suggested layout: the stage strip on top, then a vertical timeline grouped by stage (`activity.type` ↔ `stage.activityType`), each row with a checkbox to complete. Show `startDate – endDate` as the stage's window.

## 4. Activities tab — `GET /farms/{farmId}/activities`

Query: `status` (`pending` | `completed` | `skipped`), `farmCropId`, `type`, `from`, `to` (`yyyy-MM-dd`), `page` (1-based), `pageSize` (≤ 100, default 20).

```json
{ "items": [ { …activity… } ], "page": 1, "pageSize": 20, "totalCount": 9, "totalPages": 1 }
```

Order: pending first (soonest due first), then finished (latest first). Suggested segmented control: **To do** (`status=pending`) · **Done** (`status=completed`) · **All**, plus a crop filter chip row (`farmCropId`).

Add a task (`POST`) / edit (`PUT`):

```json
{ "type": "pestControl", "title": "Spray neem extract", "dueDate": "2025-07-20", "farmCropId": "c1…", "notes": null, "cost": 50 }
```

`type`, `title` (≤ 120 chars) and `dueDate` are required. `farmCropId` null = whole farm. `cost` is optional (Leones).

Complete: `POST …/activities/{id}/complete` with optional `{ "completedOn": "2025-07-15", "cost": 75, "notes": "Used 2 bags urea" }` — an empty body is fine. Skip / reopen: `POST …/skip`, `POST …/reopen` (no body).

## 5. Crop Health — `GET /farms/{farmId}/health`, `POST /farms/{farmId}/health/scans`

```json
{ "lastScan": { …scan… }, "lastScanIsFromThisFarm": true, "totalScans": 4, "history": [ { …scan… } ] }
```

**Scan Plant**: open camera/gallery, then `multipart/form-data` with `image` (JPEG/PNG ≤ 10 MB) and optional `farmCropId` (let the farmer pick which crop, defaulting to the crop being viewed). Diagnosis takes a few seconds — show a progress state. The response is one scan object; show `headline`, `confidencePercent`, `summary` and `treatmentSteps` (`order`, `title`, `description`). The scan also appears in the app's general scan history (`GET /scans`).

**View History**: list `history` (newest first) with thumbnail, date, headline and crop name.

## 6. Map tab — `GET /farms/{farmId}/map`, `PUT /farms/{farmId}/map`

```json
{
  "farmId": "6a0f…", "name": "Green Valley Farm", "district": "Port Loko",
  "center": { "latitude": 8.775, "longitude": -12.775 },
  "boundary": [ { "latitude": 8.77, "longitude": -12.78 }, { "latitude": 8.78, "longitude": -12.78 }, { "latitude": 8.78, "longitude": -12.77 } ],
  "areaHectares": 4.5,
  "plots": [ { "farmCropId": "c1…", "cropId": "rice-lowland", "cropName": "Rice (lowland)", "plotName": "Plot 1", "status": "growing", "areaHectares": 3.0, "location": null, "boundary": [] } ]
}
```

* Use `google_maps_flutter` or `flutter_map` (OpenStreetMap; no API key). Draw `boundary` as a polygon, `center` as the farm pin, each plot's `location` as a marker coloured by crop status and its `boundary` as a smaller polygon.
* `center == null` → show the district on the map and a "Set farm location" button (use the phone's GPS with `geolocator`).
* **Draw outline**: let the farmer tap corners (or walk the boundary with GPS), then `PUT /farms/{farmId}/map` with `{ "latitude", "longitude", "boundary": [3–200 points] }`. Both fields are replaced; omit to clear.
* Plot pins/outlines are saved through the crop edit (`latitude`, `longitude`, `boundary` on `PUT …/crops/{cropId}`).
* Plots without a location: list them under the map ("Not on the map yet — tap to place").

## 7. Analytics tab — `GET /farms/{farmId}/analytics`

```json
{
  "farmId": "6a0f…", "areaHectares": 4.5, "cultivatedHectares": 4.5, "landUsePercent": 100.0,
  "averageGrowthProgressPercent": 38,
  "crops": { "total": 2, "planned": 0, "growing": 2, "readyToHarvest": 0, "harvested": 0, "failed": 0 },
  "areaByCrop": [ { "cropId": "rice-lowland", "cropName": "Rice (lowland)", "hectares": 3.0, "percent": 66.7 } ],
  "activities": { "total": 14, "pending": 9, "completed": 5, "skipped": 0, "overdue": 1, "dueThisWeek": 2, "completionRatePercent": 35.7 },
  "activitiesByType": [ { "type": "fertilizing", "total": 3, "completed": 1, "cost": 450.00 } ],
  "monthly": [ { "month": "2025-02", "scheduled": 0, "completed": 0, "cost": 0 } ],
  "totalCost": 950.00, "currency": "SLE",
  "yields": [ { "cropId": "maize", "cropName": "Maize", "quantity": 1200, "unit": "kg", "harvests": 1 } ],
  "health": { "scans": 4, "healthy": 3, "diseased": 1, "healthyRatePercent": 75.0, "lastScanAt": "2025-07-10T08:01:00+00:00" }
}
```

Suggested widgets (`fl_chart` works well): KPI tiles (land use %, avg. growth %, tasks done %, total spend), donut of `areaByCrop`, bar chart of `monthly` (scheduled vs completed, last 6 months), stacked bars of `activitiesByType`, yields list, health ring (`healthyRatePercent`). `landUsePercent` is `null` when the farm size is unknown — hide that tile.

## Errors

Same shape as the rest of the API:

```json
{ "message": "Validation failed: cropId must be a crop from GET /farms/options. …", "errors": { "cropId": ["cropId must be a crop from GET /farms/options."], "plotSize": ["…"] } }
```

| Code | When | What to do |
|---|---|---|
| `400` | Invalid fields (`errors` keyed by camelCase field, e.g. `boundary`, `farmCropId`, `expectedHarvestDate`) or a bad image | Show the message under the field |
| `401` | Token missing/expired | Login, then retry |
| `404` | Farm/crop/activity not found or not yours | Refresh the list; the item was deleted |
| `500`/`502` | Server or diagnosis service problem | "Something went wrong, try again" |

## Flutter integration sketch

Models (generate with `json_serializable` / `freezed`, or hand-write):

```dart
enum CropStatus { planned, growing, readyToHarvest, harvested, failed }
enum ActivityType { landPreparation, planting, fertilizing, weeding, pestControl, harvesting, irrigation, scouting, other }
enum ActivityStatus { pending, completed, skipped }
enum StageStatus { completed, inProgress, upcoming, overdue }

class FarmCropCard {
  final String id, farmId, cropId, cropName;
  final String? localName, plotName, variety, growthStage, imageUrl, notes;
  final double? plotSizeHectares;
  final DateTime? plantingDate, expectedHarvestDate, harvestedOn;
  final bool expectedHarvestIsEstimate;
  final int? daysToHarvest;
  final CropStatus status;
  final int progressPercent, pendingActivityCount;
  // fromJson: status = CropStatus.values.byName(json['status']), dates via DateTime.tryParse
}
```

Service (using `dio`, with an interceptor that adds the `Authorization: Bearer` token stored at login — see README.mobile.md):

```dart
class MyFarmApi {
  MyFarmApi(this._dio);
  final Dio _dio;

  Future<Map<String, dynamic>> overview(String farmId, {String? calendarCropId}) async =>
      (await _dio.get('/farms/$farmId/overview',
          queryParameters: {if (calendarCropId != null) 'calendarCropId': calendarCropId})).data;

  Future<Map<String, dynamic>> addCrop(String farmId, Map<String, dynamic> body) async =>
      (await _dio.post('/farms/$farmId/crops', data: body)).data;

  Future<void> completeActivity(String farmId, String id, {double? cost}) =>
      _dio.post('/farms/$farmId/activities/$id/complete', data: {if (cost != null) 'cost': cost});

  Future<Map<String, dynamic>> scanPlant(String farmId, XFile photo, {String? farmCropId}) async =>
      (await _dio.post('/farms/$farmId/health/scans',
          data: FormData.fromMap({
            'image': await MultipartFile.fromFile(photo.path, filename: photo.name,
                contentType: DioMediaType('image', 'jpeg')),
            if (farmCropId != null) 'farmCropId': farmCropId,
          }))).data;
}
```

State: hold the selected `farmId` (e.g. Riverpod `StateProvider`, persisted with `shared_preferences`), one `FutureProvider.family` per tab keyed by `farmId`. After any change (add crop, complete task, scan, harvest) **re-fetch the overview** — it is a single cheap call and keeps the cards, calendar and upcoming list consistent. Pull-to-refresh on every tab.

Dates: `DateFormat('d MMM yyyy')` for "12 Jun 2025", `DateFormat('d MMM')` / `DateFormat('MMM')` for the calendar strip (`intl` package). Send dates as `DateFormat('yyyy-MM-dd')`.

## Design & copy notes

* Keep the design's order on the Crops tab: My Crops → Crop Calendar → Upcoming Activities → Crop Health.
* Empty states: no crops → "Add your first crop to get a planting calendar and reminders" + **Add Crop**; no upcoming tasks → "You're all caught up 🎉".
* The progress bar and growth stage are estimates from typical crop durations; a small "ⓘ Estimated" tooltip is honest copy.
* Large tap targets (≥ 48 dp) — many users work outdoors on low-end phones. Cache the last overview for offline viewing.
* Add Crop is a bottom sheet: crop (searchable, show `localName` too, e.g. "Rice — Res"), plot name, size + unit, variety, planting date (date picker), expected harvest (optional), photo.

## How it was built (backend) & database changes

Code:

| Layer | Files |
|---|---|
| Domain | `Domain/Assistant/FarmCrop.cs`, `Domain/Assistant/Farm.cs` (new fields), `Domain/Farms/*` (`FarmActivity`, `FarmScan`, enums, `IMyFarmRepository`) |
| Application | `Application/MyFarm/` — `MyFarmAppService` (all logic), `CropCalendar` (progress, stages, task planning; pure and unit-tested), `CropCatalog` (merges the harvest and fertilizer crop catalogues), `MyFarmRequestValidator`, `MyFarmDtos` |
| Infrastructure | `MyFarmRepository`, `MyFarmConfigurations.cs` (new tables), `AssistantConfigurations.cs` (new columns) |
| API | `MyFarmController`, `MyFarmCropsController`, `FarmActivitiesController` (tag **My farm**) |
| Tests | `tests/AgrismartAPI.Tests/MyFarm/` (EF InMemory; never the real database) |

Migration `Infrastructure/Persistence/Migrations/…_AddMyFarm.cs` is additive (nothing is dropped or renamed):

* `Farms` + `Boundary` (JSON text, default `[]`).
* `FarmCrops` + `PlotName`, `Variety`, `ExpectedHarvestDate`, `Status` (default `Active` for existing crops), `HarvestedOn`, `ImageUrl`, `Latitude`, `Longitude`, `Boundary` (default `[]`), `Notes`, `UpdatedAt`.
* New table `FarmActivities` (tasks; cascade-deleted with the farm, crop link set to null if the crop is deleted).
* New table `FarmScans` (links existing `PlantScans` rows to a farm/crop; `PlantScans` is unchanged).

Applying it: the API runs `Database.MigrateAsync()` on startup, so the migration is applied automatically the next time the API starts or is deployed. To apply it by hand: `dotnet ef database update` (uses the `DefaultConnection` of the current environment — **check which database that points at first**), or review and run the SQL with `dotnet ef migrations script 20260930173819_AddAiAssistant AddMyFarm`. Roll back with `dotnet ef database update 20260930173819_AddAiAssistant`.

## Checklist

- [ ] Farm switcher from `GET /farms`; empty state with "Add your farm".
- [ ] Crops tab renders entirely from `GET /farms/{farmId}/overview`.
- [ ] Crop cards: badge, variety, planted, expected harvest, progress bar, growth stage, default image per `cropId`.
- [ ] Add Crop sheet (`GET /farms/options` → `POST …/crops`), edit, photo upload, harvest, mark failed, delete.
- [ ] Calendar strip: icons, `keyDate` by `datePrecision`, ✓ for `completed`, highlight `currentStageKey`; full calendar screen.
- [ ] Upcoming activities with Today/Tomorrow/Overdue badges; complete/skip/edit/delete; Activities tab with filters and paging.
- [ ] Crop Health card + Scan Plant (multipart) + View History; empty state.
- [ ] Map tab with farm outline, plot markers and "set location"/"draw outline".
- [ ] Analytics tab with KPI tiles and charts; hide null values.
- [ ] Re-fetch overview after every change; handle `400` field errors, `401`, `404`.
