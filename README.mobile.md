# AgrismartAPI — Customer Mobile App Integration Guide (Flutter)

Audience: the **Buyer/Customer** role — browsing products, placing orders, watching tutorials, and scanning plants.

Base URLs (local dev, from `Properties/launchSettings.json`):

- HTTP: `http://localhost:5150`
- HTTPS: `https://localhost:7004`

Interactive docs while running: `/swagger` or `/scalar/v1`.

## Auth

`POST /auth/register`, `POST /auth/login`, and the two biometric-login calls below are the only
public endpoints — everything else requires a JWT.
Send it as: `Authorization: Bearer <token>`

Tokens expire in **10 minutes**. There is no refresh-token endpoint — on a `401`, send the user back to the login screen.

### POST /auth/register
Body:
```json
{ "name": "string", "email": "string", "password": "string (min 8 chars)", "role": "buyer" }
```
Always register customers with `"role": "buyer"`.
Response `200`:
```json
{ "token": "jwt", "user": { "id": "guid", "name": "string", "email": "string", "role": "buyer", "mustChangePassword": false } }
```

On success, the backend also emails the buyer a welcome message confirming their login email/password and linking to the Play Store / App Store listings — no app action needed to trigger it, but it's worth mentioning in the UI ("check your email for a copy of your login details"). `mustChangePassword` is always `false` here since the buyer chose their own password; it only ever comes back `true` for an admin-issued account (not a buyer flow).

### POST /auth/login
Body: `{ "email": "string", "password": "string" }`
Response: same `AuthResponse` shape as register.

Store the returned `token` securely (e.g. `flutter_secure_storage`) and attach it to every subsequent request.

### Biometric login (Face ID / fingerprint)

There's no biometric data sent to the API — the device proves who it is with a public/private
key pair instead. The private key is generated on-device (Android Keystore / iOS Secure
Enclave) and only ever leaves the secure hardware to *sign* something, gated behind the OS
biometric prompt (`local_auth`). The server just stores the public key and verifies signatures.

**Key requirements:** the device must generate an **EC (P-256) key pair**, non-exportable, with
biometric-gated use (Android: `setUserAuthenticationRequired(true)` on the `KeyGenParameterSpec`;
iOS: a Secure Enclave key with `.biometryCurrentSet`/`.userPresence` access control). Signatures
must be **ECDSA-SHA256, DER-encoded** — this is the native output of Android's
`Signature.getInstance("SHA256withECDSA")` and iOS's
`SecKeyCreateSignature(..., .ecdsaSignatureMessageX962SHA256, ...)`, so no extra encoding step
should be needed. Send both the public key (SubjectPublicKeyInfo/X.509 DER) and signatures as
**base64 strings**. Pick a stable `deviceId` per install (e.g. from `device_info_plus` or a
UUID you generate once and persist).

**1. Enable it** — right after a normal `POST /auth/login`, with that response's token:

`POST /auth/biometric/register` — requires `Authorization: Bearer <token>`
```json
{ "deviceId": "string", "publicKey": "base64 SubjectPublicKeyInfo (DER)" }
```
Response: `204 No Content`. Only prompt for this after the user opts in (e.g. a "use Face ID
next time?" toggle post-login) and after confirming the OS actually created the key.

**2. Log in with it** — two calls, no existing JWT needed:

`POST /auth/biometric/challenge`
```json
{ "email": "string", "deviceId": "string" }
```
Response `200`: `{ "challenge": "base64 string" }`. Returns `400` if this account/device never
registered a key — fall back to the password login screen in that case.

Decode the challenge, trigger the biometric prompt, and sign the raw challenge bytes with the
device's private key. Then:

`POST /auth/biometric/login`
```json
{ "email": "string", "deviceId": "string", "signature": "base64 string" }
```
Response: same `AuthResponse` shape as `POST /auth/login`. The challenge is single-use and
expires after **2 minutes**, so sign and submit immediately after the biometric prompt succeeds.

**Turning it off** (user disables it in settings, or you detect the on-device key was lost, e.g.
after uninstall/reinstall or the user removed all fingerprints): `DELETE /auth/biometric/{deviceId}`
— requires `Authorization: Bearer <token>`. Response: `204 No Content`.

### Reset / forgot password

There is **no "forgot password" email-link flow** — a buyer can only change their password while
logged in. Design the UI around these two cases:

**1. Buyer knows their current password** (e.g. changing it voluntarily from a settings screen):

```json
// POST /auth/change-password  (Authorize: Bearer <token>)
{ "currentPassword": "string", "newPassword": "string (min 8 chars)" }
```
`204` on success. `400` if `currentPassword` doesn't match or `newPassword` is too short.

**2. Buyer has forgotten their password** (can't log in to get a token): there's currently no
self-serve reset in the app itself. The only path is asking support/an admin to reset it for them
from the admin console — this issues a new **temporary password that's emailed to the buyer and
expires in 24 hours**. Until that support flow exists in-app, a "Forgot password?" link on the
login screen should just point the user to a support contact (email/phone), not a form.

Once the buyer has that temporary password:
- `POST /auth/login` with it succeeds normally (as long as it's used within 24 hours) and the
  response comes back with `user.mustChangePassword: true`. When you see that flag, route straight
  to a "set a new password" screen — don't let the user into the rest of the app first.
- If more than 24 hours have passed before they log in, `POST /auth/login` returns `400` ("Your
  temporary password has expired. Ask an administrator to reset it.") — they need to ask
  support/admin for a fresh one.
- To finish the reset, call the same `POST /auth/change-password` endpoint from case 1, passing the
  temporary password as `currentPassword`. `mustChangePassword` clears after this and the buyer logs
  in normally with their new password from then on. They also get a confirmation email that their
  credentials changed.

---

## Browse Products — `/products`

| Method | Path | Auth |
|---|---|---|
| GET | `/products?categoryId={guid?}` | Public |
| GET | `/products/{id}` | Public |

`MarketProductDto`:
```json
{
  "id": "guid", "name": "string", "categoryId": "guid", "categoryName": "string",
  "price": 0, "unit": "string", "sellerId": "guid", "sellerName": "string",
  "description": "string", "imageUrl": "https://.../uploads/products/xyz.jpg | null"
}
```
Use `imageUrl` directly in `Image.network(...)`.

## Browse Categories — `/categories`

| Method | Path | Auth |
|---|---|---|
| GET | `/categories` | Public |

`CategoryDto`: `{ "id": "guid", "name": "string", "description": "string | null" }`

## Browse Sellers — `/sellers`

| Method | Path | Auth |
|---|---|---|
| GET | `/sellers` | Public |
| GET | `/sellers/{id}` | Public |

Use this to show the seller/farmer's profile on a product's detail page.

`SellerDto`: `{ "id": "guid", "name": "string", "contactEmail": "string?", "contactPhone": "string?", "location": "string?", "userId": "guid?" }`

## Cart — `/cart`

One cart per customer — add products while browsing, then checkout to turn it into an order.

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/cart` | Buyer | Get (or auto-create) the caller's cart |
| POST | `/cart/items` | Buyer | Add a product; increments quantity if it's already in the cart |
| PUT | `/cart/items/{productId}` | Buyer | Set a line item's quantity to an exact value |
| DELETE | `/cart/items/{productId}` | Buyer | Remove one product from the cart |
| DELETE | `/cart` | Buyer | Empty the whole cart |
| POST | `/cart/checkout` | Buyer | Places a **cash-on-pickup or unpaid-hold** order from the cart, then empties the cart |

Add item body: `{ "productId": "guid", "quantity": 1 }`
Update item body: `{ "quantity": 2 }`

`CartItemDto`: `{ "productId", "productName", "unitPrice", "unit", "quantity", "lineTotal", "sellerId", "sellerName", "imageUrl" }`

`CartDto`: `{ "id": "guid", "items": [CartItemDto], "totalAmount": 0 }`

`POST /cart/checkout` body: `{ "paymentMethod": "cashOnPickup" | "unpaidHold" }` — see [Checkout — three ways to pay](#checkout--three-ways-to-pay) below for what each means. **Don't send `"monimeOnline"` here** — it's rejected with `400`; use `POST /checkout/sessions` instead. Returns an `OrderDto` (see Orders below) — same shape as `POST /orders`. Fails with `400` if the cart is empty or a `monimeOnline` payment method was sent here, and `404` if a product in the cart was removed from the catalogue since it was added (remove that item first).

## Checkout — three ways to pay

A purchase always starts from the cart (add items, then pick one of these). All three
place an `Order`; what differs is `paymentMethod`, whether money moves through this
app at all, and how payment eventually gets reconciled. Full backend design:
`MONIME-INTEGRATION-README.md`.

| # | Flow | How to trigger | `paymentMethod` |
|---|---|---|---|
| 1 | Pay online now (card/bank/momo, incl. **Orange Money**) via Monime, then delivery | `POST /checkout/sessions` | `monimeOnline` (implicit — no cart-checkout body needed) |
| 2 | Pay cash in person when picking up the goods | `POST /cart/checkout` or `POST /orders` | `cashOnPickup` |
| 3 | Place the order now, unpaid — price/stock held for **24 hours only** | `POST /cart/checkout` or `POST /orders` | `unpaidHold` |

For 2 and 3, show the customer plainly that #3 auto-cancels after 24 hours if unpaid
(`holdExpiresAt` on the returned `OrderDto`) — the backend cancels it automatically
(`status` flips to `cancelled`), there's no reminder/extension flow today.

### Path 1 — Pay online via Monime — `POST /checkout/sessions`

Body (both fields optional — omit entirely to use the backend's configured defaults):
```json
{ "successUrl": "agrismart://payment-success", "cancelUrl": "agrismart://payment-cancelled" }
```
Send your app's own deep links here if you want control over the post-payment
screen; otherwise the backend's configured default success/cancel pages are used.

Response `200` (`CheckoutSessionDto`):
```json
{
  "id": "guid", "orderId": "guid", "monimeSessionId": "scs-...", "status": "pending",
  "redirectUrl": "https://checkout.monime.io/...", "expireTime": "date",
  "amount": 12500.0, "currency": "SLE"
}
```

- Open `redirectUrl` in an in-app browser / WebView (or the system browser) — this
  is the **entire payment experience**; the customer picks card, bank, or mobile
  money on Monime's own hosted page (Orange Money shows up as a momo option there;
  there's nothing special to do for it on the app side).
- The session expires (`expireTime`, Monime's default is ~1 hour) if the customer
  never completes payment — the underlying order stays `pending`/unpaid until an
  admin or seller reconciles it (or, if you want the app to reflect this quickly,
  poll it — see next).
- Fails with `400` if the cart is empty; `400`/`502` if Monime itself rejects/can't
  be reached (surface `message` from the error body).

#### Full worked example (curl)

The session is built from whatever is already in the caller's cart — there is no
way to pass line items directly to `/checkout/sessions`. So a correct integration
is always at least two calls: put something in the cart, then create the session.

```bash
# 1. Log in (or register) to get a Buyer JWT
curl -X POST http://localhost:5150/auth/login \
  -H "Content-Type: application/json" \
  -d '{ "email": "buyer@example.com", "password": "Password123!" }'
# -> { "token": "eyJhbGciOi...", "user": { ... } }

TOKEN="eyJhbGciOi..."   # the token from the response above

# 2. Add at least one product to the cart — checkout has nothing to charge without this
curl -X POST http://localhost:5150/cart/items \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "productId": "c173cfac-2e3e-4d7b-bad6-76115eb30885", "quantity": 1 }'

# 3. Create the Monime checkout session — body is optional, {} is valid
curl -X POST http://localhost:5150/checkout/sessions \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "successUrl": "agrismart://payment-success", "cancelUrl": "agrismart://payment-cancelled" }'
# -> { "id": "...", "orderId": "...", "monimeSessionId": "scs-...", "status": "pending",
#      "redirectUrl": "https://checkout.monime.io/scs-...", "expireTime": "...",
#      "amount": 4500.0, "currency": "SLE" }
```

Open the response's `redirectUrl` — that's the whole payment flow from here.

**Three request-shape rules that are easy to get wrong:**
- `Authorization: Bearer <token>` is required — no anonymous/guest checkout.
- `Content-Type: application/json` must be set on the POST, even for `{}` — a
  request with no body/no content-type at all gets `415 Unsupported Media Type`,
  not `400`.
- Do **not** send `paymentMethod` here — that field belongs to `POST
  /cart/checkout`/`POST /orders` (paths 2 & 3), not this endpoint. `successUrl`/
  `cancelUrl` are the only two fields this endpoint reads, and both are optional.

**If you get a `400`, read the `message` field — it tells you exactly what's wrong:**

| `message` | Cause | Fix |
|---|---|---|
| `"Your cart is empty."` | Called `/checkout/sessions` before `/cart/items` succeeded, or with a token whose cart is empty | Confirm step 2 above returned `200` for the *same* token first |
| `"Use POST /checkout/sessions to pay online via Monime — this endpoint is for cash-on-pickup or unpaid-hold orders only."` | Sent `"paymentMethod": "monimeOnline"` to `/cart/checkout` or `/orders` instead of calling `/checkout/sessions` | Call `POST /checkout/sessions` directly for the Monime path; reserve `paymentMethod` for paths 2 & 3 |
| Anything else | Monime itself rejected the request (`400`) or couldn't be reached (`502`) | Surface the message as-is; this is not a request-shape problem on the app's side |

**`GET /checkout/sessions/{id}`** — poll the caller's own session (e.g. while the
in-app browser is open, or right after it closes) to reflect status without waiting
for an admin. Same `CheckoutSessionDto` shape; `status` is one of `pending`,
`completed`, `cancelled`, `expired` (Monime's own values — not the same enum as
`OrderDto.status`). `404` if it's not the caller's own session.

### Paths 2 & 3 — Cash-on-pickup / unpaid-hold — `POST /cart/checkout` or `POST /orders`

Covered above — just send the right `paymentMethod`. There is no "pay now" action
in the app for these; settlement happens in person and an admin marks it reconciled
on the backend (see README.admin.md's Payment Reconciliation section) — nothing
further to build on the mobile side beyond showing the order's `paymentStatus`.

## Orders — `/orders`

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/orders` | Buyer | Place a **cash-on-pickup or unpaid-hold** order directly (bypassing the cart) |
| GET | `/orders` | Buyer | The caller's own orders |
| GET | `/orders/{id}` | Buyer | The caller's own order only (404 otherwise) |

Create body:
```json
{ "items": [ { "productId": "guid", "quantity": 1 } ], "paymentMethod": "cashOnPickup" }
```
`paymentMethod` is `"cashOnPickup"` or `"unpaidHold"` — sending `"monimeOnline"` here returns `400` (use `POST /checkout/sessions` for that flow instead).

`OrderItemDto`: `{ "productId", "productName", "unitPrice", "quantity", "lineTotal", "sellerId", "sellerName" }`

`OrderDto` (returned by POST and the "my orders" list):
```json
{
  "id": "guid", "userId": "guid", "items": [OrderItemDto], "totalAmount": 0,
  "status": "pending", "createdAt": "date",
  "paymentMethod": "monimeOnline | cashOnPickup | unpaidHold",
  "paymentStatus": "pending | confirmed | paid",
  "holdExpiresAt": "date | null",
  "checkoutSessionId": "guid | null"
}
```
`status` (fulfillment) is one of: `pending, confirmed, shipped, delivered, cancelled` — **don't confuse this with `paymentStatus`** (money), which is a separate field. `holdExpiresAt` is only ever set for `unpaidHold` orders — show a countdown/expiry warning using it. `checkoutSessionId` is only set for `monimeOnline` orders; use it with `GET /checkout/sessions/{id}` to check payment progress.

`GET /orders/{id}` returns an `OrderWithCustomerDto` instead (adds a `customer` object) — harmless to ignore that field client-side, or use it to show "ordered by" in an order confirmation screen.

## Tutorials — `/tutorials`

Customers can watch, like, and comment — they cannot create/edit/delete videos (that's admin-only, see the web app guide).

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/tutorials/videos` | Authenticated | Includes `isLiked` for the caller |
| GET | `/tutorials/videos/{videoId}/comments` | Authenticated | |
| POST | `/tutorials/videos/{videoId}/like` | Authenticated | Body: `{ "liked": true }` |
| POST | `/tutorials/videos/{videoId}/comments` | Authenticated | Body: `{ "text": "string" }` |

`TutorialVideoDto`: `{ "id", "title", "description", "videoUrl", "thumbnailUrl", "instructor", "instructorAvatarEmoji", "durationLabel", "views", "uploadedAt", "likeCount", "commentCount", "isLiked" }`

`VideoCommentDto`: `{ "id", "videoId", "authorName", "text", "createdAt" }`

## Plant Scan & Diagnosis

Flow: capture/pick an image → `POST /scans` with the image (+ optional location) → the backend diagnoses
it and saves it to the user's history in one call. Use `POST /diagnoses` instead if you want a
preview-only analysis that isn't saved to history (e.g. a "retake photo?" confirmation step before
committing to a scan).

Diagnosis is backed by [Kindwise crop.health](https://crop.kindwise.com/docs), an image-recognition
API that identifies the crop in the photo plus any **pest or disease** affecting it (288 pest/disease
classes across 23 major crops). The request optionally accepts the device's location, which noticeably
improves accuracy since Kindwise can weight suggestions toward crops/pests that are actually plausible
there. (If the backend isn't configured with a Kindwise API key — e.g. a bare local dev checkout — it
silently falls back to canned sample responses, ignoring location, so these endpoints still work for UI
development.)

Both endpoints take the same `multipart/form-data` fields (max 10 MB total):

| Field | Type | Required | Notes |
|---|---|---|---|
| `image` | file | Yes | |
| `latitude` | number | No | Must be sent together with `longitude`, or omitted entirely |
| `longitude` | number | No | Must be sent together with `latitude`, or omitted entirely |

Send `latitude`/`longitude` whenever you have a location permission grant (e.g. from
`geolocator`/`location` in Flutter) — pass the device's current coordinates, not the field/farm address,
since this is about what's regionally plausible, not precise geofencing. It's fine to omit both if
location isn't available or the user declined the permission; the endpoint just skips that signal.
Sending only one of the two, or a value outside `-90..90`/`-180..180`, returns `400`.

### POST /diagnoses
Returns a `DiagnosisResultDto` — not persisted:
```json
{
  "plantName": "string",
  "diseaseName": "string | null",
  "confidence": 0.0,
  "severity": "healthy | low | moderate | high",
  "summary": "string",
  "treatmentSteps": [ { "order": 1, "title": "string", "description": "string" } ]
}
```

Notes for rendering this:
- `diseaseName` is `null` and `severity` is `"healthy"` when no pest/disease was identified with
  meaningful confidence — treat that as the "all clear" state, not an error.
- `diseaseName` may be either a **pest** (e.g. "Colorado potato beetle") or a **disease** (e.g. "Early
  blight") — the DTO doesn't currently distinguish which, so don't assume it's always a disease in copy
  like "Disease detected: {diseaseName}"; prefer neutral phrasing like "Issue detected: {diseaseName}".
- `plantName` is `"Unrecognized plant"` when the photo doesn't look like a plant/crop at all (e.g. a
  blurry or unrelated image) — prompt the user to retake the photo in that case.
- `treatmentSteps` titles are one of `"Prevention"`, `"Biological control"`, `"Chemical control"`, or a
  generic fallback — good candidates for section headers/icons in a treatment plan UI.
- `confidence` is `0.0`–`1.0`; Kindwise's own UI shows this as a percentage.

### POST /scans
Diagnoses the image (same as `/diagnoses` above) and saves it — the uploaded photo and the diagnosis —
to the caller's scan history. Returns a `PlantScanDto`:
```json
{ "id": "guid", "imagePath": "/uploads/scans/xyz.jpg", "diagnosis": { ...DiagnosisResultDto }, "scannedAt": "date" }
```

### GET /scans
The caller's own scan history — returns `PlantScanDto[]`.

## Crops & Harvest Prediction — `/crops`, `/harvest`

"When should I harvest this?" — the customer picks a crop, gives the field's location and planting
date, and the backend returns an expected harvest date window plus a growth-stage readout, backed by
real historical + forecast weather (no API key needed on the app side; the backend talks to
[Open-Meteo](https://open-meteo.com) itself).

**These endpoints are public — no login required** — so you can build the whole predictor screen
before auth is wired up, and it works for a browsing/not-yet-registered user. The one difference
logging in makes: **if you send `Authorization: Bearer <token>` on a predict call, that prediction is
saved to the caller's history** (`GET /harvest/history`); without a token, the prediction still comes
back normally, it just isn't saved anywhere. So: always attach the token when the user is logged in,
even though the endpoint doesn't require it.

### How the prediction works (useful for writing good copy/UI, not required reading to integrate)

The backend splits the season into three parts and is upfront about which one it's using for any
given answer (see `methodology.notes` and `harvest.basis`):
1. **Planting date → yesterday** — actual observed weather (reanalysis).
2. **Today → +16 days** — a real numerical weather forecast.
3. **Beyond that** — no forecast exists that far out, so it replays the last 10 years' *actual*
   weather at that location starting from the same calendar day, giving 10 plausible harvest dates.
   The 10th/50th/90th percentiles of those become `harvest.earliest` / `harvest.expected` /
   `harvest.latest` — i.e. the window width is a real statement about how variable that location's
   climate is, not an arbitrary error bar. `harvest.confidence` (`"High"`/`"Medium"`/`"Low"`) is just
   that window's width bucketed — safe to render as a badge/color directly.

If the crop's thermal requirement is already met by observed weather or is met within the 16-day
forecast, steps 3 doesn't run at all, you get a single exact date back (`windowWidthDays: 0`,
`confidence: "High"`), and `alreadyMature: true` if that date is today or earlier.

### 1. Crop catalog — `GET /crops`, `GET /crops/{cropId}`

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/crops` | Public | All crops the predictor supports |
| GET | `/crops/{cropId}` | Public | One crop; `404` if `cropId` isn't recognized |

Populate a crop picker from `GET /crops` — don't hardcode the list, it can grow over time.
`CropSummaryDto`:
```json
{
  "id": "maize",
  "name": "Maize",
  "baseTemperatureC": 10.0,
  "upperTemperatureC": 30.0,
  "gddToMaturity": 1500.0,
  "typicalDaysToMaturity": 110,
  "stages": [
    { "name": "Emergence", "startFraction": 0.0 },
    { "name": "Vegetative", "startFraction": 0.10 },
    { "name": "Flowering", "startFraction": 0.45 },
    { "name": "Grain filling", "startFraction": 0.65 },
    { "name": "Physiological maturity", "startFraction": 0.90 }
  ]
}
```
`id` is the lowercase key you pass back as `cropId` when predicting (e.g. `"maize"`,
`"rice-lowland"`, `"cassava"`) — treat it as an opaque string, not something to construct client-side.
`stages` is mainly useful for drawing a growth-stage progress bar (see `stage` below) —
`typicalDaysToMaturity` is a rough calendar-day sanity check, not what drives the actual prediction
(that's `gddToMaturity`, in growing-degree-days).

### 2. Run a prediction — `POST /harvest/predict`

Body:
```json
{
  "cropId": "maize",
  "latitude": 8.48,
  "longitude": -13.23,
  "plantingDate": "2026-06-01",
  "includeDailyTrace": false,
  "gddToMaturityOverride": null
}
```

| Field | Required | Notes |
|---|---|---|
| `cropId` | Yes | From `GET /crops` |
| `latitude` | Yes | `-90..90` |
| `longitude` | Yes | `-180..180` |
| `plantingDate` | Yes | `yyyy-MM-dd`. Must be within **3 years in the past** to **1 year in the future** — reject obviously-wrong dates client-side before calling, since the backend does too (as a `400`) |
| `includeDailyTrace` | No (default `false`) | See [Daily trace](#daily-trace--includedailytrace-true) below — leave `false` unless you're drawing a chart, the array can be 100+ points |
| `gddToMaturityOverride` | No | Only for a farmer who has their own calibrated GDD-to-maturity figure for a specific variety; when set, must be `1..20000`. Leave this out/`null` for the normal case — the crop's own `gddToMaturity` from the catalog is used |

Use the device's actual GPS coordinates (e.g. `geolocator`) for `latitude`/`longitude` when
predicting for "my current field" — or let the user pick a point on a map for a field that isn't
where they're standing right now. This is a real weather lookup, not a regional approximation, so
precision matters here more than it does for the Diagnosis endpoints' location hint.

There's also a **GET version** with the same fields as query parameters, handy for a quick manual
check from a browser address bar or a `curl` one-liner — prefer the POST above from the app itself:

`GET /harvest/predict?crop=maize&lat=8.48&lon=-13.23&planted=2026-06-01&trace=false&gddOverride=`

Response `200` — `HarvestPredictionResponseDto`:
```json
{
  "id": "fcdd9505-d77e-40e9-83be-cc802a4955b5",
  "cropId": "maize",
  "cropName": "Maize",
  "location": { "latitude": 8.48, "longitude": -13.23 },
  "plantingDate": "2026-07-01",
  "asOf": "2026-09-20",
  "alreadyMature": false,
  "gdd": {
    "accumulated": 1098.8,
    "required": 1500.0,
    "remaining": 401.2,
    "percentComplete": 73.2,
    "baseTemperatureC": 10.0,
    "upperTemperatureC": 30.0,
    "daysObserved": 82,
    "daysForecast": 16
  },
  "stage": {
    "current": "Grain filling",
    "next": "Physiological maturity",
    "nextStageStarts": "2026-10-09"
  },
  "harvest": {
    "earliest": "2026-10-18",
    "expected": "2026-10-19",
    "latest": "2026-10-19",
    "daysFromNow": 29,
    "windowWidthDays": 1,
    "confidence": "High",
    "basis": "P10/P50/P90 of 10 analog years replayed from 2026-10-07."
  },
  "methodology": {
    "model": "Modified growing-degree-day (Method B) with analog-year ensemble",
    "weatherSource": "Open-Meteo archive + forecast (no API key)",
    "analogYears": 10,
    "notes": "All analog years had complete weather coverage."
  },
  "dailyTrace": null,
  "createdAt": "2026-09-20T14:14:50.63Z"
}
```

Field-by-field, grouped by what they're for:

- **Header** — `id` (this saved prediction's id — only meaningful/reusable if the call was
  authenticated; store it if you want a "view again" link into history), `cropId`/`cropName`,
  `location`, `plantingDate`, `asOf` (the date the backend computed this as of — always "today" in
  practice, but pin your countdown math to this rather than the device clock in case they differ),
  `alreadyMature`, `createdAt`.
- **`gdd`** — the thermal-time accounting. `percentComplete` (`0..100`) is the number to drive a
  progress bar with. `accumulated`/`required`/`remaining` are the same thing in growing-degree-day
  units if you want the raw numbers too; most UIs only need `percentComplete`.
- **`stage`** — `current` is one of the crop's `stages[].name` from the catalog (or `"Pre-planting"`
  if the planting date is in the future). `next`/`nextStageStarts` are `null` once the crop has
  reached its final stage — handle that as "no further stage" rather than a missing-data bug.
- **`harvest`** — the headline answer. Always show `expected` as the primary date, and
  `earliest`–`latest` as a range underneath (e.g. "Expected Oct 19 (Oct 18 – Oct 19)"). Use
  `confidence` to pick a badge color (`"High"` green, `"Medium"` amber, `"Low"` red/gray) and surface
  `basis` as secondary/expandable text — it's a genuinely useful one-line explanation of *why* the app
  is saying what it's saying (e.g. "already met by observed weather" vs. "P10/P50/P90 of 10 analog
  years..."), good for a "why?" info icon rather than always-visible body text.
- **`methodology`** — mostly for an "About this prediction" / methodology disclosure screen, not the
  main UI. Worth surfacing `notes` when it's non-empty and mentions extrapolation (see next section) —
  it's the backend being honest that part of the answer is less certain than usual.

#### Daily trace (`includeDailyTrace: true`)

Only ask for this when you're actually drawing a GDD-accumulation chart (e.g. a line graph from
planting date to harvest) — it's an array of one point per day and can easily be 100–300+ entries.
Each point:
```json
{ "date": "2026-07-01", "maxC": 30.3, "minC": 18.4, "gdd": 14.2, "cumulative": 14.2, "source": "Observed" }
```
Plot `cumulative` against `date` for the accumulation curve; `gdd` is that single day's contribution
if you want a bar-chart view instead. **`source` is one of `"Observed"`, `"Forecast"`, or
`"Climatology"` — note the capital first letter.** Every other enum-like string in this API
(`role`, `status`, `paymentMethod`, `severity`, etc.) comes back lowercase/camelCase; this one field
is the one exception, because it's serialized as a plain string rather than through the same enum
pipeline. Match on it exactly as `"Observed"`/`"Forecast"`/`"Climatology"` — a case-insensitive
compare is the safe way to future-proof this if it's ever normalized. It's genuinely useful for
styling the chart though: solid line for `"Observed"`/`"Forecast"` (real weather), dashed/lighter for
`"Climatology"` (the projected tail, replayed from historical years) — that visually communicates
"this part is more certain than that part" for free.

When `includeDailyTrace` is omitted or `false`, `dailyTrace` comes back `null` — always null-check
before mapping over it.

### 3. Prediction history — `GET /harvest/history`

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/harvest/history` | Buyer | The caller's own saved predictions, newest first |

Same `HarvestPredictionResponseDto` shape as above, returned as an array — but **`dailyTrace` is
always `null`** here (the day-by-day trace isn't persisted, only the summary/result). If you need the
trace again for a past prediction, re-run `POST /harvest/predict` with the same inputs and
`includeDailyTrace: true`; note this computes a fresh prediction (today's weather may have moved on
since the original call) rather than replaying the exact saved one.

Only predictions made **while logged in** show up here — see the auth note at the top of this
section. A good pattern: on the predictor result screen, if the user isn't logged in, show a
"log in to save this prediction to your history" prompt rather than silently dropping it.

### Error responses

| Status | When | Example `message` |
|---|---|---|
| `400` | A validation rule was violated (see the field table above) | `"latitude must be between -90 and 90."`, `"plantingDate cannot be more than 3 years in the past."` |
| `404` | `cropId` isn't in the catalog | `"Unknown crop 'unicorn-fruit'. Call GET /crops for the list."` |
| `502` | The upstream weather service failed or returned unusable data for that location — genuinely rare, but possible for very remote coordinates or a transient outage | Surface as "couldn't fetch weather for that location — try again" rather than a generic error, since it's often transient |
| `401` | `GET /harvest/history` called without a token (predict endpoints don't require one, so this shouldn't happen there) | |

All follow the same `{ "message": "..." }` shape as the rest of the API (see
[Error format](#error-format) below).

### Worked example (curl)

```bash
# 1. Browse the catalog to populate a crop picker
curl http://localhost:5150/crops

# 2. Predict — no login needed
curl -X POST http://localhost:5150/harvest/predict \
  -H "Content-Type: application/json" \
  -d '{ "cropId": "maize", "latitude": 8.48, "longitude": -13.23, "plantingDate": "2026-06-01" }'

# 3. Same, but logged in — this one is saved to history
curl -X POST http://localhost:5150/harvest/predict \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "cropId": "maize", "latitude": 8.48, "longitude": -13.23, "plantingDate": "2026-06-01" }'

# 4. Check it's there
curl http://localhost:5150/harvest/history -H "Authorization: Bearer $TOKEN"
```

---

## Error format

Non-2xx responses return:
```json
{ "message": "human-readable reason" }
```
Status codes: `400` validation, `401` missing/expired token, `403` wrong role (e.g. a buyer hitting an admin/seller endpoint), `404` not found.
