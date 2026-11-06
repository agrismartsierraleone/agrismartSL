# AgrismartAPI — Admin & Seller Web App Integration Guide (Angular)

Audience: the **Seller/Farmer** and **Admin** roles — product management, tutorial management, and user management.

Base URLs (local dev, from `Properties/launchSettings.json`):

- HTTP: `http://localhost:5150`
- HTTPS: `https://localhost:7004`

Interactive docs while running: `/swagger` or `/scalar/v1`.

## Auth

`POST /auth/register` and `POST /auth/login` are public — everything else requires a JWT sent as `Authorization: Bearer <token>`.

Tokens expire in **10 minutes**. There's no refresh endpoint — on a `401`, redirect to login (an Angular `HttpInterceptor` + route guard is the natural place for this).

- **Buyers** self-register with `POST /auth/register`, `"role": "buyer"`. Any other role sent to this endpoint is rejected — `POST /auth/register` only ever creates buyer accounts.
- **Sellers** cannot self-register. An existing Admin creates the account via `POST /users` (`"role": "seller"`), which auto-creates a linked seller profile — the same endpoint also creates buyer accounts on an admin's behalf.
- **Admin** accounts cannot self-register either. The first admin is seeded by the backend (`admin@agrismart.local` in dev); further admins are created via `POST /users` or promoted via `PUT /users/{id}/role` by an existing admin.

```json
// POST /auth/login
{ "email": "string", "password": "string" }
```
Response:
```json
{ "token": "jwt", "user": { "id": "guid", "name": "string", "email": "string", "role": "seller | admin", "mustChangePassword": false } }
```

Decode the JWT (or use the `user.role` from login) to drive which sections of the Angular app to show — a Seller should only see Product Management for their own products; an Admin sees everything.

### First login / forced password reset

When an admin creates a seller (or any account) via `POST /users`, or resets an existing user's password via `PUT /users/{id}/credentials`, the password they set is **temporary and expires 24 hours after it's issued**. The user gets it by email along with the login URL (see [Account emails](#account-emails)).

- `POST /auth/login` with that temporary password succeeds normally **as long as it hasn't expired**, and the response has `user.mustChangePassword: true`. Route straight to a "set a new password" screen when you see this — don't let the user into the rest of the app first.
- If the 24-hour window has passed before they log in, `POST /auth/login` returns **`400`** ("Your temporary password has expired. Ask an administrator to reset it.") instead of a token — the user needs an admin to issue a fresh one via `PUT /users/{id}/credentials`.
- To complete the reset (or for any voluntary password change later), call:

```json
// POST /auth/change-password  (Authorize: Bearer <token from login>)
{ "currentPassword": "string", "newPassword": "string (min 8 chars)" }
```
`204` on success — `400` if `currentPassword` doesn't match or `newPassword` is too short. After this call, `mustChangePassword` is cleared and the temporary password stops being relevant; the user just logs in normally with their new password going forward. The user also gets a "your credentials were updated" email as confirmation.

---

## Product Management

### Products — `/products`

| Method | Path | Role | Notes |
|---|---|---|---|
| GET | `/products?categoryId={guid?}` | Any | For listing/filtering in the admin grid |
| GET | `/products/{id}` | Any | For the edit form |
| POST | `/products` | Seller, Admin | `multipart/form-data`, max 5 MB. A Seller's `sellerId` must be their own |
| PUT | `/products/{id}` | Seller (owner only), Admin | `multipart/form-data`, max 5 MB |
| DELETE | `/products/{id}` | Seller (owner only), Admin | |

A Seller gets `403` if they try to create/edit/delete a product that isn't tied to their own seller profile. Admin has no such restriction.

Create/Update form fields (`multipart/form-data`):
`name, categoryId (guid), price (decimal), unit, sellerId (guid), description, image (file, optional, .png/.jpg/.jpeg)`

`MarketProductDto` response:
```json
{
  "id": "guid", "name": "string", "categoryId": "guid", "categoryName": "string",
  "price": 0, "unit": "string", "sellerId": "guid", "sellerName": "string",
  "description": "string", "imageUrl": "https://.../uploads/products/xyz.jpg | null"
}
```

### Categories — `/categories`

Needed to populate the category dropdown on the product form.

| Method | Path | Role |
|---|---|---|
| GET | `/categories` | Any |
| POST | `/categories` | Authenticated (no extra role check today) |
| PUT | `/categories/{id}` | Authenticated |
| DELETE | `/categories/{id}` | Authenticated |

`CategoryDto`: `{ "id": "guid", "name": "string", "description": "string | null" }`
Create/Update body: `{ "name": "string", "description": "string | null" }`

### Sellers — `/sellers`

A Seller manages their own storefront profile here; an Admin creates and manages all of them.

| Method | Path | Role | Notes |
|---|---|---|---|
| GET | `/sellers` | Any | |
| GET | `/sellers/{id}` | Any | |
| POST | `/sellers` | Admin | |
| PUT | `/sellers/{id}` | Seller (owner only), Admin | |
| DELETE | `/sellers/{id}` | Seller (owner only), Admin | `409` if the seller still has products — delete/reassign products first |

`SellerDto`: `{ "id": "guid", "name": "string", "contactEmail": "string?", "contactPhone": "string?", "location": "string?", "userId": "guid?" }`
Create/Update body: `{ "name": "string", "contactEmail": "string?", "contactPhone": "string?", "location": "string?" }`

### Orders (view-only, for the seller/admin dashboard)

| Method | Path | Role | Notes |
|---|---|---|---|
| GET | `/orders/all` | Admin | Every order, with buyer + seller per line item |
| GET | `/orders/mine-as-seller` | Seller | Orders containing the caller's own products only |

`OrderWithCustomerDto` (`/orders/all`): `{ "id", "customer": { "id", "name", "email" }, "items": [OrderItemDto], "totalAmount", "status", "createdAt", "paymentMethod", "paymentStatus", "holdExpiresAt", "checkoutSessionId" }`

`OrderForSellerDto` (`/orders/mine-as-seller`): `{ "id", "customer", "items": [OrderItemDto], "subtotal", "status", "createdAt", "paymentMethod", "paymentStatus" }` — `items` is filtered to the seller's own line items and `subtotal` replaces `totalAmount`.

`OrderItemDto`: `{ "productId", "productName", "unitPrice", "quantity", "lineTotal", "sellerId", "sellerName" }`
`status` (fulfillment) is one of: `pending, confirmed, shipped, delivered, cancelled` — a separate concept from `paymentStatus` (money), see Payment Reconciliation below. `paymentMethod` is `monimeOnline | cashOnPickup | unpaidHold`. `holdExpiresAt` is only set for `unpaidHold` orders; `checkoutSessionId` only for `monimeOnline` orders.

---

## Payment Reconciliation — `/checkout/reconciliation` (Admin and Seller)

Covers all three ways a buyer can complete a purchase (see `MONIME-INTEGRATION-README.md`
§2): pay online via Monime, pay cash on pickup, or place an order unpaid (24h hold).
`paymentStatus` on an order moves `pending → confirmed → paid` regardless of which
path created it — use this section to build the "pending / confirmed / paid
transactions" view and the actions to move an order between those states.

**Scope differs by role, down to the product level:** an Admin sees every seller's
payment row on every order; a Seller only sees orders containing **at least one of
their own products** (same order-level scoping as `GET /orders/mine-as-seller`), and
even then **only their own row** within that order — never another seller's. A cart
can span multiple sellers (a multi-vendor order); each seller's portion is confirmed
and reconciled independently, so Seller A can never mark Seller B's products as paid
just because they shared a checkout. A Seller with no linked seller profile gets
`404` from every endpoint below.

### Transactions grid — `/checkout/reconciliation/transactions`

| Method | Path | Role | Notes |
|---|---|---|---|
| GET | `/checkout/reconciliation/transactions?paymentStatus=&paymentMethod=` | Admin, Seller | Every order the caller can see, with the payment row(s) they're allowed to see |
| POST | `/checkout/reconciliation/transactions/{orderId}/confirm?sellerId=` | Admin, Seller | `pending` → `confirmed` for one seller's products in the order |
| POST | `/checkout/reconciliation/transactions/{orderId}/paid?sellerId=` | Admin, Seller | → `paid` for one seller's products in the order |
| POST | `/checkout/reconciliation/transactions/{orderId}/sync` | Admin, Seller | **Monime orders only** — one lump payment, so a completed session marks every seller's row on the order paid at once (see below) |

`paymentStatus` query value is `pending`, `confirmed`, or `paid`; `paymentMethod` is `monimeOnline`, `cashOnPickup`, or `unpaidHold`. For a Seller, `paymentStatus` filters against **their own row**, not the order's aggregate — e.g. a Seller filtering "pending" sees an order even if another seller's row on it is already `paid`.

`sellerId` on `.../confirm` and `.../paid`: a **Seller's own products are always used** — the parameter is ignored for them even if sent, so a Seller can never pass someone else's seller id to act on their behalf. An **Admin must pass `sellerId`** when the order has products from more than one seller (`400` if omitted on a multi-vendor order, `400` if the given `sellerId` has no products in it); it can be omitted on a single-seller order. Both return `403` if a Seller targets an `orderId` that doesn't contain any of their products, and `409` if that seller's row isn't in the expected state (e.g. already `paid`).

`OrderPaymentDto` (returned by all four endpoints above):
```json
{
  "orderId": "guid",
  "customer": { "id": "guid", "name": "string", "email": "string" },
  "totalAmount": 0,
  "paymentMethod": "monimeOnline | cashOnPickup | unpaidHold",
  "paymentStatus": "pending | confirmed | paid",
  "orderStatus": "pending | confirmed | shipped | delivered | cancelled",
  "holdExpiresAt": "date | null",
  "checkoutSessionId": "guid | null",
  "monimeSessionId": "string | null",
  "monimeStatus": "pending | completed | cancelled | expired | null",
  "sellerPayments": [
    {
      "sellerId": "guid", "sellerName": "string", "subtotal": 0,
      "paymentStatus": "pending | confirmed | paid",
      "confirmedAt": "date | null", "confirmedByUserId": "guid | null",
      "reconciledAt": "date | null", "reconciledByUserId": "guid | null"
    }
  ],
  "createdAt": "date"
}
```

`paymentStatus` at the top level is the **order-wide aggregate**: `paid` only once
every seller on the order is paid, `pending` if any seller's row still is, otherwise
`confirmed`. `sellerPayments` is the actual breakdown the `.../confirm` and
`.../paid` actions operate on — **for an Admin this lists every seller with
products in the order; for a Seller it contains only their own entry** (other
sellers' reconciliation state, and who confirmed/reconciled it, is never exposed to
a Seller who didn't post those products).

Suggested UI: a table with a status filter (Pending / Confirmed / Paid tabs, applied
against the caller's own row for a Seller) and a payment-method column/filter. For a
multi-vendor order, render one action row per seller in `sellerPayments` — a Seller
naturally only ever sees their own. Row actions: **Confirm** (only enabled while
`pending`), **Mark Paid** (enabled once `pending` or `confirmed`), and — only for
`monimeOnline` orders where `monimeStatus` isn't `completed` yet — a **Sync with
Monime** action that calls `.../sync` to pull the latest status instead of waiting
on a webhook (Monime's checkout-session webhook isn't stable yet — see
`MONIME-INTEGRATION-README.md` §7); since Monime collects one lump sum for the
whole cart via the hosted redirect, a successful sync marks every seller's row on
that order paid together, not just the caller's. `.../sync` can return `400`/`502`
if Monime itself rejects the lookup or can't be reached.

### Raw Monime session detail — `/checkout/reconciliation/sessions` (Admin only)

Support/troubleshooting visibility into the underlying Monime call — "what actually
happened to this customer's payment," independent of the reconciliation grid above.
Unlike the transactions grid, this is **Admin-only** — it isn't scoped per seller and
spans every order on the platform, so it isn't exposed to Sellers.

| Method | Path | Role | Notes |
|---|---|---|---|
| GET | `/checkout/reconciliation/sessions` | Admin | Every checkout session ever created |
| GET | `/checkout/reconciliation/sessions/{id}` | Admin | One session's full detail, incl. every create/sync attempt against it |

`CheckoutSessionAdminDto`:
```json
{
  "id": "guid", "orderId": "guid", "userId": "guid", "monimeSessionId": "scs-...",
  "status": "pending | completed | cancelled | expired",
  "redirectUrl": "string?", "cancelUrl": "string?", "successUrl": "string?",
  "expireTime": "date?", "idempotencyKey": "string", "rawResponseJson": "string (raw Monime payload)",
  "createdAt": "date", "lastSyncedAt": "date?",
  "auditLogs": [
    {
      "id": "guid", "orderId": "guid?", "userId": "guid?", "idempotencyKey": "string",
      "requestJson": "string", "responseStatusCode": 0, "responseJson": "string?",
      "success": true, "errorMessage": "string?", "checkoutSessionId": "guid?", "createdAt": "date"
    }
  ]
}
```
`auditLogs` includes the original create attempt and every later `.../sync` call against this session — read top-to-bottom (newest first) for a timeline of what Monime returned each time.

---

## Tutorial Management — `/tutorials` (Admin only)

| Method | Path | Role | Notes |
|---|---|---|---|
| GET | `/tutorials/videos` | Any authenticated | For the listing grid |
| POST | `/tutorials/videos` | **Admin** | `multipart/form-data`, max 200 MB |
| PUT | `/tutorials/videos/{videoId}` | **Admin** | `multipart/form-data`, max 200 MB |
| DELETE | `/tutorials/videos/{videoId}` | **Admin** | |
| GET | `/tutorials/videos/{videoId}/comments` | Any authenticated | For moderation view |

Create/Update form fields: `title, description, videoUrl (optional if videoFile given), videoFile (file, optional), thumbnailUrl, instructor, instructorAvatarEmoji, durationLabel`

`TutorialVideoDto`: `{ "id", "title", "description", "videoUrl", "thumbnailUrl", "instructor", "instructorAvatarEmoji", "durationLabel", "views", "uploadedAt", "likeCount", "commentCount", "isLiked" }`

Sellers get `403` on create/update/delete — tutorial content is Admin-only.

---

## User Management — `/users` (Admin only)

| Method | Path | Role |
|---|---|---|
| GET | `/users` | Admin |
| POST | `/users` | Admin |
| PUT | `/users/{id}/role` | Admin |
| PUT | `/users/{id}/credentials` | Admin |
| DELETE | `/users/{id}` | Admin |

`UserSummaryDto`: `{ "id", "name", "email", "phoneNumber": "string | null", "nin": "string | null", "role", "createdAt", "mustChangePassword", "passwordExpiresAt": "iso date | null" }`
Create user body: `{ "name", "email", "password" (min 8 chars), "role": "buyer | seller | admin", "phoneNumber" (max 30 chars), "nin" (max 50 chars) }` — `phoneNumber` and `nin` are required for buyers and sellers (`400` if missing or blank) and optional for admins.
Change role body: `{ "role": "buyer | seller | admin" }`
Update credentials body: `{ "name": "string?", "email": "string?", "newPassword": "string? (min 8 chars)", "phoneNumber": "string?", "nin": "string?" }` — send only the fields being changed; omitted/blank fields are left as-is. Returns `200` with the updated `UserSummaryDto` even if nothing changed (no email/audit entry is generated in that case). Setting `newPassword` here always issues it as a fresh 24-hour temporary password (`mustChangePassword` flips back to `true`) — same as a brand-new account.

Use `mustChangePassword` / `passwordExpiresAt` in the users grid to show something like "Pending first login (expires in 6h)" for accounts still on their temporary password — helpful for an admin chasing up a seller who hasn't logged in yet.

`phoneNumber` and `nin` are `null` for accounts created before these fields existed. Use `PUT /users/{id}/credentials` to fill them in.

`DELETE /users/{id}` permanently removes the account (`204` on success). Two guardrails to surface in the UI, both come back as normal error responses (see Error format below), not crashes:
- **`400`** — an admin cannot delete their own account. Disable/hide the delete action on the currently-logged-in admin's own row.
- **`409`** — the user still has a linked seller profile. The admin must delete (or reassign) the seller profile via `/sellers/{id}` first — surface the message and link to the seller record rather than retrying.

`POST /users` is how an admin onboards a seller (or buyer) account directly — creating a user with `"role": "seller"` here, or promoting one to `"seller"` via the role endpoint, auto-creates a linked seller profile for them if one doesn't already exist.

### Account emails

The backend sends transactional email (via SendGrid) on the account actions below — no frontend work is needed to trigger them, but the UI should set expectations (e.g. "an email with login details has been sent to the seller"):

| Trigger | Email sent to | Contents |
|---|---|---|
| `POST /users` (any role, incl. seller) | the new user | Their email, the temporary password just set, their role, an access link, and a note that it expires in 24 hours |
| `PUT /users/{id}/role` (role actually changes) | the affected user | Notice that their role changed, and an access link |
| `PUT /users/{id}/credentials` (any field actually changes) | the affected user | A bullet list of what changed (name/email/password/phone number/NIN) and an access link. A password reset also includes the new temporary password. The phone number and NIN values are never included in the email or the audit log. If the email address changed, the previous address gets the same notice, without the password |
| `PUT /sellers/{id}` (any field actually changes, whether an admin or the seller made the update) | the linked user, or the profile's contact email (old and new) if it has no linked account | A bullet list of what changed (name/contact email/contact phone/location) and an access link. No email is sent if the profile has no linked account and no contact email |
| `DELETE /sellers/{id}` | the linked user, or the profile's contact email if it has no linked account | Notice that the seller profile was deleted |
| `POST /auth/change-password` | the user themselves | Confirmation that their password was changed (sent even though they made the change) |
| `DELETE /users/{id}` | the deleted user | Notice that their account was deleted and they can no longer log in |

Emails are best-effort: the change has already been saved when the email is sent, so if SendGrid rejects it or can't be reached, the API still returns success and the failure is only recorded in the server logs. If a user says an email never arrived, check the logs. For a lost temporary password, issue a fresh one with `PUT /users/{id}/credentials`.

"Access link" is role-aware: Sellers and Admins get the web login URL (they use this admin app); a Buyer created via `POST /users` gets Play Store / App Store links instead, since buyers use the mobile app. Buyers who self-register via `POST /auth/register` (mobile-only, see `README.mobile.md`) get the same welcome-with-credentials email, but without the temporary-password/24h-expiry language, since they chose their own permanent password.

There's no "resend welcome email" endpoint — if a seller says they didn't get it (or their temporary password expired), an admin can re-issue one by making a `PUT /users/{id}/credentials` call with a `newPassword` — this sets a fresh 24-hour temporary password and emails it to them.

---

## Audit Log — `/audit-logs` (Admin only)

Read-only activity trail for compliance/support — surface it as a simple admin-only table (e.g. filterable by date/action) rather than exposing it to sellers.

| Method | Path | Role |
|---|---|---|
| GET | `/audit-logs` | Admin |

`AuditLogDto`: `{ "id", "actorUserId": "guid?", "actorEmail", "action", "targetUserId": "guid?", "details": "string?", "createdAt" }`

Returned newest-first, unpaginated today. `action` is one of a fixed set of strings — treat it as an enum for filtering/badges in the UI:

`UserRegistered, UserLoggedIn, UserCreated, UserRoleChanged, UserCredentialsUpdated, UserPasswordReset, UserDeleted, SellerCreated, SellerUpdated, SellerDeleted`

`UserPasswordReset` is logged when the user completes their own password change via `POST /auth/change-password` (the actor and target are the same user) — distinct from `UserCredentialsUpdated`, which is an admin acting on someone else's account via `PUT /users/{id}/credentials`.

`actorUserId`/`actorEmail` identify who performed the action (for self-service actions like login/register, the actor is the user themselves); `targetUserId` identifies the affected user, when applicable.

---

## Market Prices — `/market-prices` (ticker for Seller and Admin)

Daily prices of **crops, fertilizers and seeds** by district, so farmers know a fair price before buying or selling. There are three pieces of frontend work:

1. **A scrolling ticker** along the bottom of every page, for sellers and admins, like the news strip on a TV channel. The code is below.
2. **A Market Prices page** for sellers and admins, showing the board with category, name and district filters.
3. **A price entry form** on that page, for admins only.

| Method | Path | Role | Purpose |
|---|---|---|---|
| GET | `/market-prices/ticker` | Public | Prices for the ticker, in every category |
| GET | `/market-prices?category=&crop=&district=&date=` | Public | The price board for one category |
| GET | `/market-prices/pdf?category=&crop=&district=&date=` | Public | The same board as a PDF file |
| GET | `/market-prices/history?category=&crop=&district=&days=30` | Public | One item in one district, for a chart |
| POST | `/market-prices` | **Admin** | Record one day's prices |
| DELETE | `/market-prices/{id}` | **Admin** | Remove a price entered by mistake |

The GET endpoints don't need a token, so a background refresh can never return `401`. This matters because `auth.interceptor.ts` logs the user out on any `401`, and tokens only last 10 minutes.

Prices come only from admins entering them. There is no outside price feed, so the ticker stays hidden until an admin records the first prices.

### Categories

| `category` | Example names |
|---|---|
| `crop` (the default) | `Maize`, `Local Rice` |
| `fertilizer` | `NPK 15-15-15`, `Urea` |
| `seed` | `Maize`, `NERICA 4` |

- **The name is still in `crop`:** for a fertilizer or a seed, the `crop` field holds its name, such as `Urea`. The field wasn't renamed, so existing code keeps working.
- **The same name can be in two categories:** `Maize` can be both a crop and a seed, with separate prices and separate history.
- **The default is `crop`:** the board, PDF, history and `POST` all use `crop` when `category` is left out, so the current page keeps working before these changes are made. The ticker is the exception: it shows every category.

### Adding fertilizers and seeds to the web app

The page and ticker already exist, so they need these changes. The code below hasn't been compiled against the app, so treat it as a guide.

**1. `core/models/market-price.model.ts`:** add the category.

```ts
export type MarketPriceCategory = 'crop' | 'fertilizer' | 'seed';

export const MARKET_PRICE_CATEGORIES: { value: MarketPriceCategory; label: string; plural: string }[] = [
  { value: 'crop', label: 'Crop', plural: 'crops' },
  { value: 'fertilizer', label: 'Fertilizer', plural: 'fertilizers' },
  { value: 'seed', label: 'Seed', plural: 'seeds' },
];

// Add to both MarketPrice and MarketPriceEntry:
category: MarketPriceCategory;
```

**2. `core/services/market-prices.service.ts`:** send the category, and build the PDF link.

```ts
board(filters: { category: MarketPriceCategory; district?: string; date?: string }): Observable<MarketPriceBoard> {
  let params = new HttpParams().set('category', filters.category);
  if (filters.district) params = params.set('district', filters.district);
  if (filters.date) params = params.set('date', filters.date);
  return this.http.get<MarketPriceBoard>(this.base, { params });
}

/** The PDF is public, so a plain link downloads it without going through HttpClient. */
pdfUrl(filters: { category: MarketPriceCategory; district?: string; date?: string }): string {
  let params = new HttpParams().set('category', filters.category);
  if (filters.district) params = params.set('district', filters.district);
  if (filters.date) params = params.set('date', filters.date);
  return `${this.base}/pdf?${params.toString()}`;
}
```

**3. `features/market-prices/market-prices-page/market-prices-page.ts`:**

- Add `readonly categoryFilter = signal<MarketPriceCategory>('crop');`.
- Add the category to the `query` computed, and pass it to `this.marketPrices.board(...)`, so the board reloads when it changes.
- When the category changes, clear `cropFilter`, because a fertilizer name means nothing on the seed board. Leave the category out of `hasFilters` and `clearFilters`: it's a tab, not a filter.
- In `newRow`, add a `category` control, defaulting to `categoryFilter()`.
- In `submit`, add `category: row.category` to each entry.
- In `addRow`, carry `last.category` to the new row, as it does for district and unit.

**In `openCorrect`, keep the item's category and disable the control, like crop and district.** This is the one change that must not be missed. If a correction reaches `POST` without the category, the API treats it as a crop, so it adds a new crop row and leaves the wrong fertilizer price in place.

```ts
const row = this.newRow(item.crop, item.district, item.unit, item.price, item.category);
row.controls.category.disable();
```

`submit` already uses `getRawValue()`, so a disabled category is still sent.

**4. `market-prices-page.html`:**

- Add three tabs above the filters, from `MARKET_PRICE_CATEGORIES`.
- Change the `Crop` filter label, the `Search crops` placeholder and the `Crop` table heading to follow the selected category.
- In the form, add a category select to each row, and relabel the `Crop` field `Name`.
- Show the `crop-options` suggestions only on crop rows, because `GET /crops` has no fertilizers or seeds. Type those names in.
- Update the empty message and the hint under the form, which only mention crops.
- Add the download link:

```html
<a class="btn" [href]="marketPrices.pdfUrl({ category: categoryFilter(), district: districtFilter(), date: dateFilter() })">
  Download PDF
</a>
```

`marketPrices` is `private` in the component, so make it `readonly` for the template to reach it. The PDF follows the category, district and date. The name search box filters in the browser, so it doesn't affect the PDF.

**5. `shared/market-ticker/market-ticker.html` (optional):** the ticker already shows fertilizers and seeds by name. To make them clearer, show the category beside any that isn't a crop:

```html
<strong>{{ item.crop }}</strong>
@if (item.category !== 'crop') {
  <span class="ticker-tag">{{ item.category }}</span>
}
```

**Order of release:** deploy the API first. It adds the `AddMarketPriceCategory` migration, and existing prices become `crop`. Because the API works without `category`, the web app can follow later.

### Reading prices

`GET /market-prices/ticker` and `GET /market-prices` both return:

```json
{
  "currency": "SLE",
  "asOf": "2026-11-05",
  "items": [
    {
      "id": "guid",
      "category": "crop",
      "crop": "Maize",
      "district": "Bo",
      "price": 450.5,
      "unit": "50 kg bag",
      "priceDate": "2026-11-05",
      "previousPrice": 420,
      "previousDate": "2026-11-04",
      "change": 30.5,
      "changePercent": 7.3,
      "trend": "up"
    }
  ]
}
```

- **`trend`:** `up`, `down`, `flat` or `new`. `new` means there is no earlier price to compare with, so `previousPrice`, `previousDate`, `change` and `changePercent` are `null`.
- **Comparisons:** a price is only compared with an earlier one in the same unit. Changing from `50 kg bag` to `kg` gives `new`, not a false price drop.
- **`priceDate`:** this can be older than `asOf` when a district has no price yet today. Always show the date in that case, so an old price never looks like today's.

|  | Ticker | Board |
|---|---|---|
| Shows | Latest price per item and district, in every category | Latest price per item and district, in one category |
| Oldest price | 7 days | 30 days |
| Order | Newest first, then name, then district | Name, then district |
| Limit | 60 items | None |

Board filters are all optional:

- `category` is `crop`, `fertilizer` or `seed`, and defaults to `crop`. Anything else returns `400`.
- `crop` is the name of the crop, fertilizer or seed. It matches the whole name and ignores upper and lower case.
- `district` also accepts a town such as `Freetown`, but an unknown district returns `400`.
- `date` (`yyyy-MM-dd`) shows the board as it stood on that day.

`GET /market-prices/history` needs `crop` and `district`, `days` can be 1 to 365, and `category` defaults to `crop`. Points are oldest first:

```json
{ "category": "crop", "crop": "Maize", "district": "Bo", "currency": "SLE", "points": [{ "date": "2026-11-04", "price": 420, "unit": "50 kg bag" }] }
```

The unit can change between points, so check it before drawing them on one line.

`GET /market-prices/pdf` takes the same filters as the board and downloads an A4 PDF, titled `Crop prices`, `Fertilizer prices` or `Seed prices`, with a file name such as `agrismart-fertilizer-prices-2026-11-05.pdf`. Each file covers one category. If nothing matches it still returns `200`, with a PDF saying no prices were recorded.

### Recording prices (Admin)

```json
// POST /market-prices
{
  "priceDate": "2026-11-05",
  "prices": [
    { "category": "crop", "crop": "Maize", "district": "Bo", "price": 450.5, "unit": "50 kg bag" },
    { "category": "fertilizer", "crop": "NPK 15-15-15", "district": "Bo", "price": 850, "unit": "50 kg bag" },
    { "category": "seed", "crop": "Maize", "district": "Kenema", "price": 120, "unit": "kg" }
  ]
}
```

Response: `{ "priceDate": "2026-11-05", "created": 2, "updated": 1 }`

| Field | Rule |
|---|---|
| `priceDate` | Optional, defaults to today. Past dates are allowed for catching up, future dates return `400` |
| `prices` | 1 to 200 entries, which can mix categories. A name and district can appear only once per category in a request |
| `category` | Optional, defaults to `crop`. Must be `crop`, `fertilizer` or `seed` |
| `crop` | Required, at most 100 characters. The name of the crop, fertilizer or seed. Stored in title case, so `maize` and `MAIZE` are the same item. `NPK`, `DAP`, `MOP`, `TSP`, `SSP`, `CAN` and `NERICA` stay in capitals |
| `district` | Required, a Sierra Leone district. Use `GET /logistics/districts` for the dropdown |
| `price` | Greater than 0, at most 1,000,000,000, in Leones (SLE). Rounded to 2 decimal places |
| `unit` | Required, at most 50 characters. Keep it the same for a crop, so the change can be worked out |

- **All or nothing:** if one entry is invalid, nothing is saved. The `400` message names the row, for example `prices[3]: price must be greater than 0 and at most 1000000000.`
- **Corrections:** sending a category, name, district and date again replaces that price, so a typo can be fixed by resending the row. Always send the category: without it, a fertilizer or seed correction creates a new crop price instead.
- **Deleting:** `DELETE /market-prices/{id}` returns `204`, using the `id` from the board. The earlier price then becomes the latest.
- **Crop names:** `appSqlSafe` blocks `(`, `)` and `,`. If the form uses it, offer names without them (`Local Rice`, not `Rice (Local)`). `GET /crops` has crop names to start from, but it includes some with brackets, and `crop` accepts any name, such as `Palm Oil`. `NPK 15-15-15` passes `appSqlSafe` because it has single hyphens, but `--` is blocked.

### Adding the ticker

The code below compiled against this app and ran inside its shell. It keeps a steady speed of about 60 pixels a second for any number of prices. Because it goes in the shell, it appears on every signed-in page for sellers and admins, and not on the login page.

In `src/app/layout/shell/shell.html`, add it straight after `</main>`:

```html
    <main class="content container">
      <router-outlet />
    </main>

    <app-market-ticker />
```

Then add `MarketTicker` to the `imports` array in `shell.ts`. Because it sits inside `.shell-main`, it stays at the bottom of the screen without covering the sidebar.

`src/app/core/models/market-price.model.ts`

```ts
export type PriceTrend = 'new' | 'up' | 'down' | 'flat';

export interface MarketPrice {
  id: string;
  crop: string;
  district: string;
  price: number;
  unit: string;
  priceDate: string;
  previousPrice: number | null;
  previousDate: string | null;
  change: number | null;
  changePercent: number | null;
  trend: PriceTrend;
}

export interface MarketPriceBoard {
  currency: string;
  asOf: string;
  items: MarketPrice[];
}
```

`src/app/core/services/market-prices.service.ts`

```ts
import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/app-config';
import { MarketPriceBoard } from '../models/market-price.model';

@Injectable({ providedIn: 'root' })
export class MarketPricesService {
  private readonly base = `${API_BASE_URL}/market-prices`;

  constructor(private readonly http: HttpClient) {}

  ticker(): Observable<MarketPriceBoard> {
    return this.http.get<MarketPriceBoard>(`${this.base}/ticker`);
  }
}
```

`src/app/shared/market-ticker/market-ticker.ts`

```ts
import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, switchMap, timer } from 'rxjs';
import { MarketPricesService } from '../../core/services/market-prices.service';

const REFRESH_MS = 15 * 60 * 1000;
const SECONDS_PER_ITEM = 6;
const MIN_LOOP_SECONDS = 30;

@Component({
  selector: 'app-market-ticker',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './market-ticker.html',
  styleUrl: './market-ticker.css',
})
export class MarketTicker {
  private readonly marketPrices = inject(MarketPricesService);

  // A failed refresh keeps the prices already on screen.
  readonly board = toSignal(
    timer(0, REFRESH_MS).pipe(
      switchMap(() => this.marketPrices.ticker().pipe(catchError(() => EMPTY))),
    ),
  );

  readonly items = computed(() => this.board()?.items ?? []);
  readonly loopSeconds = computed(() =>
    Math.max(MIN_LOOP_SECONDS, this.items().length * SECONDS_PER_ITEM),
  );
  readonly paused = signal(false);
}
```

`src/app/shared/market-ticker/market-ticker.html`

```html
@if (board(); as board) {
  @if (items().length > 0) {
    <section class="ticker" aria-label="Market prices">
      <span class="ticker-label">Market prices</span>

      <div class="ticker-viewport">
        <div
          class="ticker-track"
          [class.paused]="paused()"
          [style.animation-duration.s]="loopSeconds()"
        >
          <!-- The list is drawn twice so the loop has no gap. Screen readers get it once. -->
          @for (copy of [0, 1]; track copy) {
            <ul class="ticker-list" [attr.aria-hidden]="copy === 1 ? 'true' : null">
              @for (item of items(); track item.id) {
                <li class="ticker-item">
                  <strong>{{ item.crop }}</strong>
                  <span class="district">{{ item.district }}</span>
                  <span>Le {{ item.price | number: '1.0-2' }} / {{ item.unit }}</span>
                  @switch (item.trend) {
                    @case ('up') {
                      <span class="trend up"><span aria-hidden="true">▲</span> up {{ item.changePercent }}%</span>
                    }
                    @case ('down') {
                      <span class="trend down"><span aria-hidden="true">▼</span> down {{ -item.changePercent! }}%</span>
                    }
                    @case ('flat') {
                      <span class="trend">no change</span>
                    }
                  }
                  @if (item.priceDate !== board.asOf) {
                    <span class="date">{{ item.priceDate | date: 'd MMM' }}</span>
                  }
                </li>
              }
            </ul>
          }
        </div>
      </div>

      <button
        type="button"
        class="ticker-toggle"
        [attr.aria-label]="paused() ? 'Play market prices' : 'Pause market prices'"
        (click)="paused.set(!paused())"
      >
        {{ paused() ? '▶' : '❚❚' }}
      </button>
    </section>
  }
}
```

`src/app/shared/market-ticker/market-ticker.css`

```css
:host {
  display: block;
  position: sticky;
  bottom: 0;
  z-index: 20;
}

/* The band stays dark in both themes, so it reads as a fixed broadcast strip. */
.ticker {
  --band: hsl(154 60% 13%);
  display: flex;
  align-items: center;
  height: 2.5rem;
  background: var(--band);
  color: hsl(140 25% 94%);
  font-size: 0.875rem;
}

.ticker-label {
  flex-shrink: 0;
  padding-inline: 1rem;
  line-height: 2.5rem;
  background: var(--brand-gold);
  color: hsl(154 60% 10%);
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  white-space: nowrap;
}

.ticker-viewport {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

/* Two equal columns: moving left by exactly one column looks like an endless loop. */
.ticker-track {
  display: grid;
  grid-template-columns: 1fr 1fr;
  width: max-content;
  min-width: 200%;
  animation: ticker-scroll linear infinite;
}

.ticker-track.paused,
.ticker-track:hover {
  animation-play-state: paused;
}

@keyframes ticker-scroll {
  to {
    transform: translateX(-50%);
  }
}

.ticker-list {
  display: flex;
  justify-content: space-around;
  margin: 0;
  padding: 0;
  list-style: none;
}

.ticker-item {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  padding-inline: 1.5rem;
  white-space: nowrap;
}

.district,
.date {
  color: hsl(140 14% 78%);
}

.trend {
  color: hsl(140 14% 78%);
  font-weight: 600;
}

.trend.up {
  color: hsl(140 70% 72%);
}

.trend.down {
  color: hsl(354 90% 82%);
}

.ticker-toggle {
  flex-shrink: 0;
  width: 2.5rem;
  height: 2.5rem;
  border: 0;
  background: var(--band);
  color: inherit;
  cursor: pointer;
}

.ticker-toggle:focus-visible {
  outline: 2px solid var(--brand-gold);
  outline-offset: -2px;
}

/* No motion: the prices become a strip the user scrolls by hand. */
@media (prefers-reduced-motion: reduce) {
  .ticker-viewport {
    overflow-x: auto;
  }
  .ticker-track {
    grid-template-columns: 1fr;
    min-width: 100%;
    animation: none;
  }
  .ticker-list[aria-hidden='true'],
  .ticker-toggle {
    display: none;
  }
}

@media (max-width: 560px) {
  .ticker {
    font-size: 0.8125rem;
  }
  .ticker-label {
    padding-inline: 0.625rem;
    font-size: 0.6875rem;
  }
}
```

The ticker behaves like this:

- **Pausing:** it pauses on hover and has a pause button, which touch screens need and accessibility rules (WCAG 2.2.2) require for moving text.
- **Reduced motion:** with that setting on, it stops moving and can be scrolled by hand.
- **No prices:** it hides itself completely.
- **Refreshing:** it updates every 15 minutes. If a refresh fails, the current prices stay on screen. The interceptor's "Cannot reach the AgriSmart API" toast can still appear when the network is down.
- **Currency:** it shows `Le`, like the rest of the admin app.

---

## Error format

Non-2xx responses return:
```json
{ "message": "human-readable reason" }
```
Status codes: `400` validation, `401` missing/expired token, `403` wrong role or not the owner, `404` not found, `409` conflict (e.g. deleting a seller with products).

## Role matrix (quick reference)

| Area | Seller | Admin |
|---|---|---|
| Own products (CRUD) | ✅ | ✅ (any) |
| Own seller profile (update/delete) | ✅ | ✅ (any) |
| Create sellers | ❌ | ✅ |
| Categories (CRUD) | ✅ (not restricted today) | ✅ |
| Tutorials (CRUD) | ❌ | ✅ |
| Users / roles / credentials | ❌ | ✅ |
| Audit log | ❌ | ✅ |
| View all orders | ❌ | ✅ |
| View own orders (as seller) | ✅ | ✅ (via `/orders/all`) |
| Payment reconciliation — transactions grid, confirm/paid/sync | ✅ (own products only, never another seller's) | ✅ (any) |
| Payment reconciliation — raw session/audit detail | ❌ | ✅ |
| Market prices ticker, board and PDF (view) | ✅ | ✅ |
| Market prices (record/delete) | ❌ | ✅ |
| Farm jobs: browse, worker profile and apply | ✅ | ❌ (web page is seller-only for now) |
| Farm jobs: post, close, delete, view applicants | ❌ | ✅ |
