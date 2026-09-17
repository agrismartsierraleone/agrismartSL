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
{ "token": "jwt", "user": { "id": "guid", "name": "string", "email": "string", "role": "seller | admin" } }
```

Decode the JWT (or use the `user.role` from login) to drive which sections of the Angular app to show — a Seller should only see Product Management for their own products; an Admin sees everything.

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

`OrderWithCustomerDto` (`/orders/all`): `{ "id", "customer": { "id", "name", "email" }, "items": [OrderItemDto], "totalAmount", "status", "createdAt" }`

`OrderForSellerDto` (`/orders/mine-as-seller`): same shape, but `items` is filtered to the seller's own line items and `subtotal` replaces `totalAmount`.

`OrderItemDto`: `{ "productId", "productName", "unitPrice", "quantity", "lineTotal", "sellerId", "sellerName" }`
`status` is one of: `pending, confirmed, shipped, delivered, cancelled`.

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

`UserSummaryDto`: `{ "id", "name", "email", "role", "createdAt" }`
Create user body: `{ "name", "email", "password" (min 8 chars), "role": "buyer | seller | admin" }`
Change role body: `{ "role": "buyer | seller | admin" }`
Update credentials body: `{ "name": "string?", "email": "string?", "newPassword": "string? (min 8 chars)" }` — send only the fields being changed; omitted/blank fields are left as-is. Returns `200` with the updated `UserSummaryDto` even if nothing changed (no email/audit entry is generated in that case).

`POST /users` is how an admin onboards a seller (or buyer) account directly — creating a user with `"role": "seller"` here, or promoting one to `"seller"` via the role endpoint, auto-creates a linked seller profile for them if one doesn't already exist.

### Account emails

The backend sends transactional email (via SendGrid) on the account actions below — no frontend work is needed to trigger them, but the UI should set expectations (e.g. "an email with login details has been sent to the seller"):

| Trigger | Email sent to | Contents |
|---|---|---|
| `POST /users` (any role, incl. seller) | the new user | Their email, the plaintext password just set, their role, and the login URL |
| `PUT /users/{id}/role` (role actually changes) | the affected user | Notice that their role changed, and the login URL |
| `PUT /users/{id}/credentials` (any field actually changes) | the affected user | A bullet list of what changed (name/email/password) and the login URL |

There's no "resend welcome email" endpoint — if a seller says they didn't get it, an admin can trigger an equivalent email by making a no-op-looking `PUT /users/{id}/credentials` call with a `newPassword` (this both resets their password and re-sends credentials-style notice).

---

## Audit Log — `/audit-logs` (Admin only)

Read-only activity trail for compliance/support — surface it as a simple admin-only table (e.g. filterable by date/action) rather than exposing it to sellers.

| Method | Path | Role |
|---|---|---|
| GET | `/audit-logs` | Admin |

`AuditLogDto`: `{ "id", "actorUserId": "guid?", "actorEmail", "action", "targetUserId": "guid?", "details": "string?", "createdAt" }`

Returned newest-first, unpaginated today. `action` is one of a fixed set of strings — treat it as an enum for filtering/badges in the UI:

`UserRegistered, UserLoggedIn, UserCreated, UserRoleChanged, UserCredentialsUpdated, SellerCreated, SellerUpdated, SellerDeleted`

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
