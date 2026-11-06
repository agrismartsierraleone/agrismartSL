# Frontend Guide: Latest API Changes

This guide lists what each app needs to change. Find your app below and work through that part. You only need the other part if you want to see how the two connect.

| You're working on | Who uses it | Go to |
|---|---|---|
| **Mobile app** (Flutter, `AgriSmart-UI`) | Buyers | [Part 1: Mobile app](#part-1-mobile-app-buyers) |
| **Web app** (Angular, `AgriSmartSLAdmin`) | Sellers and admins | [Part 2: Web app](#part-2-web-app-sellers-and-admins) |

Both apps also need [Release status](#release-status) and [Conventions](#conventions). For the full endpoint reference, see `README.mobile.md` (mobile), `README.admin.md` (web) and `README.aitools.md` (AI tools, logistics and jobs).

## Who gets what

| Feature | Buyer (mobile) | Seller (web) | Admin (web) |
|---|---|---|---|
| Phone number and NIN | Enter at registration | - | Create, view and edit on users |
| Account emails | - | - | Tell the admin the user is notified |
| Delivery details | Enter at checkout | - | - |
| Delivery cost estimate | Check an estimate | - | - |
| Order tracking | Follow the timeline | Update status of own orders | Update status of any order |
| AI assistant chat | Use it | - | - |
| AI listing and price suggestion | - | On the product form | On the product form |
| Earnings summary | - | On the dashboard | - |
| Delivery plan | - | Own orders | All orders |
| Order alerts | - | - | Review them |
| Farm jobs | Browse and apply | Browse and apply | Post, close, delete, view applicants |
| Market prices | View prices and download a PDF | Ticker and read-only page | Ticker, page and price entry |

## Release status

| Change | Status |
|---|---|
| Phone number and NIN | **Live in production. The apps need this update now.** |
| Account emails | Live in production. Admin wording only |
| Everything else | **Not deployed yet.** Backend developers can run it locally. Don't release these screens until the backend confirms the deployment |

The AI features also need an Anthropic key on the server. It isn't set in production yet, so check `GET /api/assistant/status` and hide AI features when it returns `{ "enabled": false }`.

## Conventions

- **Base URL:** `https://srv2117244.hstgr.cloud`
- **Sign-in:** send `Authorization: Bearer <token>`. Tokens last 10 minutes, and a `401` means sending the user back to log in.
- **JSON:** field names and enum values are camelCase, for example `"role": "buyer"` and `"status": "shipped"`.
- **Currency:** amounts are in SLE.

### Errors

```json
{ "message": "Text you can show the user" }
```

Some `400` responses also name the field:

```json
{ "message": "district must be a Sierra Leone district.", "errors": { "district": ["district must be a Sierra Leone district."] } }
```

| Status | Meaning | What the app should do |
|---|---|---|
| `400` | Invalid input | Show `message`, or mark the fields in `errors` |
| `401` | Not signed in or token expired | Go to login |
| `403` | Not allowed | Show `message` |
| `404` | Not found, or an AI feature is switched off | Show a not-found state |
| `409` | Not possible right now | Show `message` |
| `429` | Daily AI limit reached | Show `message` and disable AI buttons until tomorrow |
| `503` | AI is temporarily unavailable | Let the user fill the form in by hand, or try again |

Malformed JSON or an unknown enum value returns ASP.NET's standard `400` format, without our `message` field. Keep a generic error message as a fallback.

### All AI features

- Hide them when `GET /api/assistant/status` returns `enabled: false`.
- Show a loading state, as AI replies can take several seconds.
- Run them when the user taps a button, not on page load. Chat and AI tools share a limit of 30 requests per user per day.
- Handle `429` and `503` without losing what the user typed.
- Let the user edit every draft before saving.

---

# Part 1: Mobile app (buyers)

The mobile app is for buyers: registering, ordering, following deliveries, using the assistant and finding farm work.

| # | Screen | What to do | Release |
|---|---|---|---|
| [M1](#m1-register-phone-number-and-nin) | Register | Add phone number and NIN | **Now** |
| [M2](#m2-checkout-delivery-details) | Checkout | Add delivery district and address | After deployment |
| [M3](#m3-checkout-or-product-details-delivery-cost-estimate) | Checkout or product details | Show a delivery estimate | After deployment |
| [M4](#m4-order-details-tracking) | Order details | Show a tracking timeline | After deployment |
| [M5](#m5-assistant-chat-suggested-prompts) | Assistant chat | Add suggested prompts | After deployment |
| [M6](#m6-farm-jobs) | Jobs list, job details, worker profile | Browse jobs, apply, create a profile | After deployment |

**Don't build these on mobile.** They're for the web app, and a buyer gets `403` from them:

- Posting, closing or deleting jobs, or viewing applicants
- Updating an order's delivery status
- AI listing, price and earnings tools, delivery plans and order alerts

The assistant can answer seller earnings questions, but that only applies to seller accounts, so don't suggest it to buyers.

## M1. Register: phone number and NIN

**This is a breaking change.** Registration returns `400` until the app sends both fields.

Add required phone number (max 30 characters) and NIN (max 50 characters) fields.

`POST /auth/register`

```json
{
  "name": "Aminata Kamara",
  "email": "aminata@example.com",
  "password": "at least 8 characters",
  "role": "buyer",
  "phoneNumber": "+23276123456",
  "nin": "1234567"
}
```

The errors are `"Phone number is required."` and `"NIN is required."`. Only presence and length are checked, not the format.

NIN is sensitive, so don't include it in logs or analytics. Login and registration responses don't include these fields, and there's no endpoint yet for users to view or edit their own.

## M2. Checkout: delivery details

1. Load the district list from `GET /logistics/districts` (public) and show a dropdown.
2. Add a delivery address field (max 300 characters).
3. Send both when placing the order.

`POST /orders`

```json
{
  "items": [{ "productId": "guid", "quantity": 2 }],
  "paymentMethod": "cashOnPickup",
  "deliveryDistrict": "Kenema",
  "deliveryAddress": "12 Hangha Road"
}
```

`POST /checkout/sessions` accepts the same `deliveryDistrict` and `deliveryAddress` next to its existing fields.

Both are optional in the API. An order without a district can't be grouped into a delivery trip, so ask for it whenever goods are being delivered.

## M3. Checkout or product details: delivery cost estimate

`POST /logistics/delivery-estimate`

```json
{ "fromDistrict": "Bo", "toDistrict": "Kenema", "weightKg": 50 }
```

```json
{
  "fromDistrict": "Bo",
  "toDistrict": "Kenema",
  "approximateDistanceKm": 79.6,
  "weightKg": 50,
  "estimatedCost": 1234.00,
  "currency": "SLE",
  "note": "This is a rough estimate from district to district, not a final price."
}
```

Always show it as an estimate. It isn't added to the order total.

**This returns `409` until delivery rates are set**, so hide the feature on that response. Products don't store a weight, so the user has to enter one.

## M4. Order details: tracking

Order responses now include:

```json
{
  "status": "shipped",
  "deliveryDistrict": "Kenema",
  "deliveryAddress": "12 Hangha Road",
  "shippedAt": "2026-11-05T10:15:00+00:00",
  "deliveredAt": null
}
```

The steps are `pending`, `confirmed`, `shipped`, `delivered`. A `cancelled` order shows no further steps. Use `shippedAt` and `deliveredAt` to build the timeline. They can be `null` on older orders.

There's no notification to the buyer yet when the status changes, so refresh the order when it's opened.

## M5. Assistant chat: suggested prompts

No API changes are needed. `POST /api/assistant/chat` can now answer questions about products, the user's orders and payments, delivery costs and farm jobs. It can look things up but can't pay for, cancel or change anything.

Suggested prompts to show:

- "Find rice near Bo"
- "Where is my order?"
- "Has my payment gone through?"
- "Are there any farm jobs in Kenema?"

Replies can contain Markdown, such as bold text and lists, so render it or strip it.

## M6. Farm jobs

Any signed-in user can browse jobs, create a worker profile and apply. Only admins can post, so **remove any "post a job" screen from the mobile app**. `POST /jobs` returns `403` for buyers.

| Action | Request |
|---|---|
| List open jobs | `GET /jobs?district=Kenema&skill=harvesting` (filters are optional) |
| View a job | `GET /jobs/{id}` |
| Apply | `POST /jobs/{id}/apply` |
| View own worker profile | `GET /jobs/worker-profile` (`404` if not created yet) |
| Save own worker profile | `PUT /jobs/worker-profile` |

### Job details

Show these fields from the job response:

```json
{
  "id": "guid",
  "title": "Rice harvesters",
  "description": "Harvest two acres.",
  "district": "Kenema",
  "workersNeeded": 5,
  "skills": ["harvesting"],
  "startDate": "2026-12-01",
  "pay": "Agreed daily",
  "contactPhone": "+23276000000",
  "contactEmail": "jobs@example.com",
  "status": "open"
}
```

`startDate`, `pay`, `contactPhone` and `contactEmail` can each be `null`, so hide that row when they are. Tapping the phone or email can open the phone's dialler or email app.

### Worker profile

```json
{
  "headline": "Harvest worker",
  "bio": "Ten seasons of rice harvesting.",
  "district": "Kenema",
  "skills": ["harvesting"],
  "isAvailable": true
}
```

Limits: headline 200 characters, bio 1000. Only workers with `isAvailable: true` appear in matches.

To help workers write one, ask a few simple questions and send the answers to `POST /api/ai/worker-profile-draft`:

```json
{
  "experience": "ten seasons harvesting and weeding rice",
  "skills": "harvesting, weeding",
  "district": "Kenema",
  "availability": "weekdays"
}
```

```json
{
  "headline": "Rice harvesting and weeding worker from Kenema",
  "bio": "I have ten seasons of experience...",
  "district": "Kenema",
  "skills": ["Rice harvesting", "Rice weeding"]
}
```

Send `experience` or `skills`, at least. Let the worker edit the result, then save it with `PUT /jobs/worker-profile`.

### Apply

`POST /jobs/{id}/apply` with `{ "message": "optional, up to 500 characters" }` returns `204`.

| Situation | Response |
|---|---|
| No worker profile | `400`: send the user to create one |
| Own job | `400`: hide the button |
| Already applied | `409` |
| Job is closed | `409` |

Applying shares the worker's phone number with admins, so say this on the apply screen. The API has no list of a worker's own applications yet, so handle the `409` to show they've already applied.

## Mobile checklist

**Release now**

- [x] M1: phone number and NIN at registration

**Release after the backend is deployed**

- [ ] M2: delivery district and address at checkout
- [ ] M3: delivery estimate, hidden on `409`
- [ ] M4: order tracking timeline
- [ ] M5: suggested prompts in chat
- [ ] M6: jobs list and details, including the contact email
- [ ] M6: worker profile and applying
- [ ] M6: no "post a job" screen

---

# Part 2: Web app (sellers and admins)

The web app is for sellers and admins. Sign-in decides what each person sees, so check the role on every screen. A hidden button isn't enough on its own, but it avoids showing users a `403`.

| # | Screen | Seller | Admin | Release |
|---|---|---|---|---|
| [W1](#w1-users-phone-number-and-nin) | Users | No access | Add phone number and NIN | **Now** |
| [W2](#w2-users-and-sellers-account-email-wording) | Users, Sellers | No wording needed | Say the user is notified | **Now** |
| [W3](#w3-orders-delivery-status) | Orders | Update own orders | Update any order | After deployment |
| [W4](#w4-product-form-ai-listing-and-price) | Product form | AI listing and price | AI listing and price | After deployment |
| [W5](#w5-dashboard-earnings-summary) | Dashboard | Earnings summary | Hide it | After deployment |
| [W6](#w6-deliveries-delivery-plan) | Deliveries (new) | Delivery plan | Delivery plan | After deployment |
| [W7](#w7-order-alerts) | Order alerts (new) | Hide it | Review alerts | After deployment |
| [W8](#w8-farm-jobs) | Jobs (new) | Browse, apply, worker profile | Post and manage jobs | After deployment |
| [W9](#w9-market-prices) | Market prices (new) and ticker | View | View and enter prices | After deployment |

**New sidebar items:** in `NAV_ITEMS` in `shell.ts`, set `adminOnly: true` on Order alerts. Deliveries and Market Prices are for both roles. Jobs is `sellerOnly: true` for now, with `sellerGuard` on its route. When the admin screens are built, remove both and show each role its own view.

**Don't build these on the web app.** They're for the mobile app:

- Placing orders and checkout. Both are buyer-only and return `403` for sellers and admins
- The delivery estimate and the assistant chat

## W1. Users: phone number and NIN

**Admin only. This is a breaking change.** Creating buyers or sellers returns `400` until the form sends both fields.

- **Create user:** `POST /users` takes `phoneNumber` (max 30 characters) and `nin` (max 50 characters). They are required for buyers and sellers and optional for admins, so show them as required once the role is `buyer` or `seller`.
- **Users list:** `GET /users` now returns `phoneNumber` and `nin`. Existing accounts have `null`, so show an empty state such as "Not provided".
- **Edit user:** `PUT /users/{id}/credentials` accepts optional `phoneNumber` and `nin`. Use it to fill in details for existing accounts.
- **Blank values:** send only the fields being changed. A blank value is ignored, so these fields can't be cleared.

NIN is sensitive. Show it only in admin screens, and don't include it in logs or analytics.

## W2. Users and Sellers: account email wording

The backend now emails users automatically. Nothing needs to be triggered, but tell the person making the change, for example "The user will be notified by email."

- **Profile changes:** updating a user through `PUT /users/{id}/credentials`, or a seller profile through `PUT /sellers/{id}`, sends an email listing what changed.
- **Email changes:** both the old and new addresses are told.
- **Password resets:** the temporary password is now in the email. Admins don't need to pass it on themselves.
- **Deleted seller profiles:** the seller is told.

Email is best-effort, so a successful response doesn't prove the email arrived. If a temporary password goes missing, the admin can set a new one.

## W3. Orders: delivery status

**Seller:** orders from `GET /orders/mine-as-seller`. **Admin:** orders from `GET /orders/all`.

Orders now include `deliveryDistrict`, `deliveryAddress`, `shippedAt` and `deliveredAt`, so show them in the order details. The last two can be `null` on older orders.

`PUT /orders/{id}/delivery-status`

```json
{ "status": "shipped" }
```

The steps are `pending`, `confirmed`, `shipped`, `delivered`. Show only the steps ahead of the current one. Steps can be skipped, but they can't be undone, so ask for confirmation first.

| Situation | Response | What to show |
|---|---|---|
| Moving backwards or repeating a step | `409` | Hide those options |
| Order is cancelled | `409` | Hide the action |
| Not paid, and `paymentMethod` isn't `cashOnPickup` | `409` | Disable the action while `paymentStatus` is `pending` |
| Seller updating an order that includes other sellers' items | `403` | Show `message`. An admin must update it |

`GET /orders/mine-as-seller` returns only that seller's items, so the app can't tell in advance that an order includes other sellers. Handle the `403` when it happens.

## W4. Product form: AI listing and price

**Seller and admin.**

### Write a listing with AI

`POST /api/ai/listing-draft` (`multipart/form-data`)

| Field | Notes |
|---|---|
| `notes` | Text about the product, up to 2000 characters |
| `image` | Optional `.png` or `.jpg` photo, up to 5 MB |

Send at least one of them.

```json
{
  "title": "Clean Local Rice, 50kg Bags",
  "description": "Clean local rice from Bo, harvested this season. Sold in 50kg bags.",
  "categoryId": "guid or null",
  "categoryName": "Produce",
  "unit": "bag"
}
```

Fill the product form with this for the seller to edit. Nothing is saved until they submit the form as usual. The draft has no price, and `categoryId` or `unit` may be `null`.

### Suggest a price

`POST /api/ai/price-suggestion`

```json
{ "productName": "rice", "categoryId": "guid (optional)", "unit": "bag (optional)" }
```

```json
{
  "productName": "rice",
  "suggestedPrice": 120.00,
  "lowestPrice": 100.00,
  "highestPrice": 150.00,
  "comparableListings": 3,
  "comparableSales": 0,
  "explanation": "Rice prices here range from 100 to 150..."
}
```

The three price fields are `null` when there isn't enough data. In that case, show only `explanation`. Offer the price as something to accept, and let the seller change it.

## W5. Dashboard: earnings summary

**Seller only.** Admins get `403`, so hide it for them.

`GET /api/ai/earnings-summary?days=30` (`days` from 1 to 366)

```json
{
  "earnings": {
    "days": 30,
    "orders": 1,
    "received": 0,
    "confirmed": 0,
    "pending": 200.00,
    "topProducts": [{ "productName": "Upland rice", "quantity": 2, "revenue": 200.00 }]
  },
  "currency": "SLE",
  "summary": "Over the last 30 days, you had 1 order..."
}
```

Show the figures as cards with `summary` underneath. A `404` means the account has no seller profile.

## W6. Deliveries: delivery plan

**Seller and admin.** This is a new page.

`GET /api/ai/delivery-plan?fromDistrict=Bo`

`fromDistrict` is optional. For sellers, it defaults to their profile location.

```json
{
  "fromDistrict": "Bo",
  "trips": [
    {
      "district": "Kenema",
      "approximateDistanceKm": 79.6,
      "stops": [
        {
          "orderId": "guid",
          "customerName": "Buyer Name",
          "customerPhone": "+23276000000",
          "address": "12 Hangha Road",
          "items": [],
          "status": "pending"
        }
      ]
    }
  ],
  "missingDistrict": [],
  "advice": "Text, or null"
}
```

- Show `trips` in the order given, nearest first. `approximateDistanceKm` is `null` when no starting district is known.
- List `missingDistrict` separately, as those orders need a destination.
- Hide `advice` when it's `null`. The plan still works without AI.
- The page contains customer contact details, so keep it to signed-in sellers and admins.

## W7. Order alerts

**Admin only.** Sellers get `403`, so hide the sidebar item. This is a new page.

`GET /orders/alerts`

```json
[
  {
    "orderId": "guid",
    "customer": { "id": "guid", "name": "Name", "email": "email" },
    "totalAmount": 5000.00,
    "status": "pending",
    "createdAt": "2026-11-05T10:15:00+00:00",
    "reasons": ["The same buyer placed the same items again within an hour."]
  }
]
```

Use wording such as "Worth reviewing", never "fraud". An alert doesn't change the order, and there's no dismiss action, so an order stays listed while it's within the 30-day window.

## W8. Farm jobs

This is a new page, and each role gets a different view.

| | Seller | Admin |
|---|---|---|
| Browse open jobs | Yes | Yes |
| Create a worker profile and apply | Yes | No |
| Post, close or delete a job | No, returns `403` | Yes |
| View applicants and suggested workers | No, returns `403` | Yes |

### Seller view

The seller view is built in `src/app/features/jobs/jobs-page`. A seller can:

1. Browse open jobs and filter by skill or district.
2. See each job's pay, phone number and email.
3. Create or edit their worker profile.
4. Apply, with an optional message of up to 500 characters.

| Action | Request |
|---|---|
| List open jobs | `GET /jobs?district=Kenema` |
| View own worker profile | `GET /jobs/worker-profile` (`404` if not created yet) |
| Save own worker profile | `PUT /jobs/worker-profile` |
| Apply | `POST /jobs/{id}/apply` |

The worker profile and apply request formats are in [M6](#m6-farm-jobs), and the page behaves like this:

- **No profile yet:** Apply opens the profile form, then goes straight to the application for that job.
- **After applying:** the job shows an Applied badge. The API has no list of a worker's own applications, so the badge is lost on reload, and applying again shows "already applied".
- **Job closed meanwhile:** the `409` message is shown and the list refreshes.
- **Privacy:** the application form says that applying shares the seller's phone number with admins.
- **Skills:** these are added one at a time, because `appSqlSafe` blocks commas.

### Admin view

**Admin only, and not built yet.**

| Action | Request |
|---|---|
| List open jobs | `GET /jobs?district=Kenema&skill=harvesting` (filters are optional) |
| View a job | `GET /jobs/{id}` |
| Post a job | `POST /jobs` |
| Close a job | `POST /jobs/{id}/close` |
| Delete a job | `DELETE /jobs/{id}` |
| View applicants | `GET /jobs/{id}/applicants` |
| View suggested workers | `GET /api/ai/jobs/{id}/matches` |

#### Post a job

```json
{
  "title": "Rice harvesters",
  "description": "Harvest two acres.",
  "district": "Kenema",
  "workersNeeded": 5,
  "skills": ["harvesting"],
  "startDate": "2026-12-01",
  "pay": "Agreed daily",
  "contactPhone": "+23276000000",
  "contactEmail": "jobs@example.com"
}
```

`title`, `description`, `district` and `workersNeeded` (1 to 1000) are required. Limits: title 200 characters, description 2000, pay 200, contact phone 30, contact email 254, and up to 20 skills of 50 characters each.

`contactEmail` is optional and comes back on every job. An invalid address returns `400`, and a blank one is saved as `null`.

`contactPhone` and `contactEmail` are visible to every signed-in user, so say that on the form.

#### Write the post with AI

`POST /api/ai/job-draft`

```json
{ "notes": "need 5 people to harvest rice in Kenema next week" }
```

```json
{
  "title": "Rice Harvest Workers Needed in Kenema",
  "description": "We need 5 people to harvest rice next week...",
  "district": "Kenema",
  "workersNeeded": 5,
  "skills": ["Rice harvesting"]
}
```

Fill the post form with this for the admin to review. `district` and `workersNeeded` may be `null`. Pay is never included, so the admin always enters it.

#### Applicants and suggested workers

`GET /jobs/{id}/applicants`

```json
[
  {
    "applicationId": "guid",
    "workerUserId": "guid",
    "name": "Worker Name",
    "phoneNumber": "+23276000000",
    "headline": "Harvest worker",
    "district": "Kenema",
    "skills": ["harvesting"],
    "message": "I can start Monday",
    "appliedAt": "2026-11-05T10:15:00+00:00"
  }
]
```

`GET /api/ai/jobs/{id}/matches`

```json
[
  {
    "workerUserId": "guid",
    "name": "Worker Name",
    "headline": "Harvest worker",
    "district": "Kenema",
    "skills": ["harvesting"],
    "score": 5,
    "reason": "Has ten seasons of rice harvesting experience and lives in Kenema."
  }
]
```

Matches don't include phone numbers, and there's no way yet to invite a suggested worker. Show them as "Suggested workers", and keep contact details to the applicants list. An empty list means no one suits the job yet.

## W9. Market prices

| Piece | Seller | Admin |
|---|---|---|
| Scrolling ticker at the bottom of every page | Yes | Yes |
| Market Prices page with filters | Read-only | Yes |
| Price entry form, and deleting a price | Hide it | Yes |

The ticker reads `GET /market-prices/ticker`, the page reads `GET /market-prices`, and admins save with `POST /market-prices`. Reading prices doesn't need a token. Sellers get `403` from the save and delete calls.

Prices appear only once an admin enters them, because there's no outside price feed. The ticker hides itself until then.

For the request and response formats and the ticker code, see the Market Prices section of `README.admin.md`.

## Web checklist

**Release now**

- [x] W1: phone number and NIN when creating, viewing and editing users
- [ ] W2: tell admins that users are notified by email

**Release after the backend is deployed**

- [ ] W3: delivery status actions, and delivery details on orders
- [ ] W4: AI listing and price buttons on the product form
- [ ] W5: earnings summary, for sellers only
- [ ] W6: Deliveries page
- [ ] W7: Order alerts page, for admins only
- [ ] W8: seller Jobs page with browsing, worker profile and applying (built, not yet committed)
- [ ] W8: admin job posting with the contact email, applicants and suggested workers
- [ ] W9: market prices ticker and page
