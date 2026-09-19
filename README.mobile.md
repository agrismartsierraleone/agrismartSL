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
  "amount": 12500.0, "currency": "SLE",
  "ussdCode": "*715*18284#", "ussdCodeExpireTime": "date"
}
```

- Open `redirectUrl` in an in-app browser / WebView (or the system browser) so the
  customer can complete payment on Monime's hosted page — pick from card, bank,
  or mobile money there (Orange Money shows up as a momo option on Monime's page;
  there's nothing special to do for it on the app side).
- `ussdCode` is a **real, one-time dial string generated by Monime specifically for
  this order** (not a fixed merchant code — the digits differ every time). Offer it
  as an equal alternative to the redirect, e.g. "Pay by dialing `*715*18284#` and
  following the prompts" — either completing payment fulfills the same order.
  `ussdCode` can come back `null` if Monime's payment-code call failed; in that case
  only the redirect option is available for this session. `ussdCodeExpireTime` is
  usually later than the checkout session's own `expireTime` — show whichever is
  sooner as "this offer expires at…" if you display a single countdown.
- The session/code expire (`expireTime`/`ussdCodeExpireTime`) if the customer never
  completes payment — the underlying order stays `pending`/unpaid until an admin
  reconciles it (or, if you want the app to reflect this quickly, poll it — see
  next).
- Fails with `400` if the cart is empty; `400`/`502` if Monime itself rejects/can't
  be reached (surface `message` from the error body).

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

---

## Error format

Non-2xx responses return:
```json
{ "message": "human-readable reason" }
```
Status codes: `400` validation, `401` missing/expired token, `403` wrong role (e.g. a buyer hitting an admin/seller endpoint), `404` not found.
