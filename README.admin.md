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
  "ussdCode": "string | null",
  "paymentCodeStatus": "pending | processing | completed | expired | cancelled | null",
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
a Seller who didn't post those products). `ussdCode` is the real, per-transaction
USSD dial string Monime generated for this order (e.g. `*715*18284#`) — worth
showing in the grid/detail view for support to read back to a customer who says "I
paid by USSD but it still shows pending." `paymentCodeStatus` is that payment
code's own lifecycle, independent of `monimeStatus` (the checkout session's) —
either reaching `completed` is what `.../sync` treats as paid.

Suggested UI: a table with a status filter (Pending / Confirmed / Paid tabs, applied
against the caller's own row for a Seller) and a payment-method column/filter. For a
multi-vendor order, render one action row per seller in `sellerPayments` — a Seller
naturally only ever sees their own. Row actions: **Confirm** (only enabled while
`pending`), **Mark Paid** (enabled once `pending` or `confirmed`), and — only for
`monimeOnline` orders where neither `monimeStatus` nor `paymentCodeStatus` is
`completed` yet — a **Sync with Monime** action that calls `.../sync` to pull the
latest status from both the checkout session and the payment code instead of
waiting on a webhook (Monime's checkout-session webhook isn't stable yet — see
`MONIME-INTEGRATION-README.md` §7); since Monime collects one lump sum for the
whole cart, a successful sync marks every seller's row on that order paid together,
not just the caller's. `.../sync` can return `400`/`502` if Monime itself rejects
the lookup or can't be reached.

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
  "paymentCodeId": "string?", "ussdCode": "string?", "paymentCodeStatus": "string?", "paymentCodeExpireTime": "date?",
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
`auditLogs` includes the checkout-session create attempt, the (separate) payment-code create attempt, and every later `.../sync` call against this session — read top-to-bottom (newest first) for a timeline of what Monime returned each time. `paymentCodeId`/`ussdCode`/`paymentCodeStatus` are null only if the payment-code create call failed (non-fatal — the redirect flow still worked); check `auditLogs` for that failed attempt's `errorMessage`.

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

`UserSummaryDto`: `{ "id", "name", "email", "role", "createdAt", "mustChangePassword", "passwordExpiresAt": "iso date | null" }`
Create user body: `{ "name", "email", "password" (min 8 chars), "role": "buyer | seller | admin" }`
Change role body: `{ "role": "buyer | seller | admin" }`
Update credentials body: `{ "name": "string?", "email": "string?", "newPassword": "string? (min 8 chars)" }` — send only the fields being changed; omitted/blank fields are left as-is. Returns `200` with the updated `UserSummaryDto` even if nothing changed (no email/audit entry is generated in that case). Setting `newPassword` here always issues it as a fresh 24-hour temporary password (`mustChangePassword` flips back to `true`) — same as a brand-new account.

Use `mustChangePassword` / `passwordExpiresAt` in the users grid to show something like "Pending first login (expires in 6h)" for accounts still on their temporary password — helpful for an admin chasing up a seller who hasn't logged in yet.

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
| `PUT /users/{id}/credentials` (any field actually changes) | the affected user | A bullet list of what changed (name/email/password) and an access link |
| `POST /auth/change-password` | the user themselves | Confirmation that their password was changed (sent even though they made the change) |
| `DELETE /users/{id}` | the deleted user | Notice that their account was deleted and they can no longer log in |

"Access link" is role-aware: Sellers and Admins get the web login URL (they use this admin app); a Buyer created via `POST /users` gets Play Store / App Store links instead, since buyers use the mobile app. Buyers who self-register via `POST /auth/register` (mobile-only, see `README.mobile.md`) get the same welcome-with-credentials email, but without the temporary-password/24h-expiry language, since they chose their own permanent password.

There's no "resend welcome email" endpoint — if a seller says they didn't get it (or their temporary password expired), an admin can re-issue one by making a `PUT /users/{id}/credentials` call with a `newPassword` — this both sets a fresh 24-hour temporary password and re-sends a credentials-style notice.

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
