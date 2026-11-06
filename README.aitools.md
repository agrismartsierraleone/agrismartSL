# AI for Trade, Payments, Logistics and Jobs

The API for the marketplace AI tools, delivery tracking and the farm jobs board.

All endpoints need `Authorization: Bearer <token>` unless marked public.

## How the AI is used

- **Figures come from the backend.** Prices, earnings, distances and costs are calculated in code. The AI only writes the words around them.
- **Drafts are not saved.** A listing, job or profile draft is returned for the user to edit, then saved with the normal endpoint.
- **Buyers' details stay private.** Names, phone numbers and addresses are not sent to the AI provider.
- **Failures are safe.** Where the backend already has the figures, a failed AI call just leaves out the AI text. Pure AI drafts return `503`.

Everything under `/api/ai` follows the `Features:AiAssistant` flag (`404` when off), needs `Assistant:ApiKey`, and counts towards the assistant's daily limit per user (`429` when reached).

## Trade

### POST /api/ai/listing-draft (Seller, Admin)

Multipart form with `notes` and/or `image` (.png or .jpg, up to 5 MB).

Returns `{ "title", "description", "categoryId": "guid?", "categoryName": "string?", "unit": "string?" }`. It never includes a price.

### POST /api/ai/price-suggestion (Seller, Admin)

Body: `{ "productName": "string", "categoryId": "guid?", "unit": "string?" }`

Returns `{ "productName", "suggestedPrice", "lowestPrice", "highestPrice", "comparableListings", "comparableSales", "explanation" }`.

The suggestion is the median of similar listings and their past sales. The three price fields are `null` when there are fewer than 3 comparable prices.

### Assistant: finding products

`POST /api/assistant/chat` can now answer questions like "find rice near Bo", using live marketplace data.

## Payments

### Assistant: payment and order questions

`POST /api/assistant/chat` can now answer "has my payment gone through?" and "where is my order?". It reads only the caller's own orders and cannot change anything.

### GET /api/ai/earnings-summary?days=30 (Seller)

Returns `{ "earnings": { "days", "orders", "received", "confirmed", "pending", "topProducts": [{ "productName", "quantity", "revenue" }] }, "currency", "summary" }`.

Only the seller's own items count, and cancelled orders are left out.

### GET /orders/alerts (Admin)

Returns `[{ "orderId", "customer", "totalAmount", "status", "createdAt", "reasons": ["string"] }]` for the last 30 days.

An order is flagged when:

- it is at least 5 times a typical order (needs 10 or more past orders)
- the same buyer placed the same items again within an hour
- the buyer is also the seller of an item
- the buyer has 3 or more cancelled orders in 30 days

These are fixed rules rather than AI, so each flag can be explained. Alerts only inform an admin. They never cancel an order or block a buyer, and a flag is not proof of fraud.

## Logistics

### Delivery details on orders

`POST /orders` and `POST /checkout/sessions` now accept optional `deliveryDistrict` and `deliveryAddress` (max 300 characters). An unknown district returns `400`. Town names such as "Freetown" or "Makeni" are saved as their district.

Order responses now include `deliveryDistrict`, `deliveryAddress`, `shippedAt` and `deliveredAt`.

### GET /logistics/districts (public)

Returns the list of district names.

### PUT /orders/{id}/delivery-status (Seller, Admin)

Body: `{ "status": "confirmed | shipped | delivered" }`

- Steps can be skipped, but never undone. Going backwards returns `409`.
- Orders must be paid first (`409`), except `cashOnPickup`.
- A seller can update only orders made up entirely of their own items. An order with several sellers needs an admin (`403`).
- Each change is written to the audit log as `OrderDeliveryStatusChanged`.

### GET /api/ai/delivery-plan?fromDistrict=Bo (Seller, Admin)

Returns `{ "fromDistrict", "trips": [{ "district", "approximateDistanceKm", "stops": [...] }], "missingDistrict": [...], "advice" }`.

It covers orders that are not yet shipped and are either paid or cash on pickup, nearest district first. Sellers see only their own items, and `fromDistrict` defaults to the seller's location. `missingDistrict` lists orders with no destination. `advice` is `null` if the AI is unavailable.

### POST /logistics/delivery-estimate

Body: `{ "fromDistrict": "string", "toDistrict": "string", "weightKg": 10 }`

Returns `{ "fromDistrict", "toDistrict", "approximateDistanceKm", "weightKg", "estimatedCost", "currency", "note" }`.

The cost is `BaseFee + RatePerKm x distance + RatePerKg x weight`. It is an estimate only and is not added to the order total. The assistant can also use it.

**This needs real rates.** There are none by default, so it returns `409` until they are set:

```json
"Delivery": {
  "BaseFee": 0,
  "RatePerKm": 0,
  "RatePerKg": 0,
  "RoadDistanceFactor": 1.3,
  "Currency": "SLE"
}
```

Distances use each district's main town and a road factor, so they are approximate.

## Jobs

### Job board

| Method | Path | Who |
|---|---|---|
| GET | `/jobs?district=&skill=` | Any signed-in user |
| POST | `/jobs` | Admin |
| GET | `/jobs/{id}` | Any signed-in user |
| POST | `/jobs/{id}/close` | Admin |
| DELETE | `/jobs/{id}` | Admin |
| POST | `/jobs/{id}/apply` | Buyers and sellers with a worker profile |
| GET | `/jobs/{id}/applicants` | Admin |
| GET | `/jobs/worker-profile` | The user themselves |
| PUT | `/jobs/worker-profile` | Buyers and sellers, for themselves |

Job body: `{ "title", "description", "district", "workersNeeded", "skills": ["string"], "startDate": "yyyy-MM-dd?", "pay": "string?", "contactPhone": "string?", "contactEmail": "string?" }`

Worker profile body: `{ "headline", "bio", "district", "skills": ["string"], "isAvailable": true, "phoneNumber": "string?", "nin": "string?" }`

`phoneNumber` and `nin` update the worker's account, and leaving one out keeps the current value. `GET` and `PUT` return `phoneNumber`, `email` and `nin`. The email is read-only here because it is the login, and passwords are not part of the worker profile.

Apply body: `{ "message": "string?" }`. Applying twice or to a closed job returns `409`.

A worker's phone number and email are shared only with admins, through the applicants list of a job they apply to.

### POST /api/ai/job-draft (Admin)

Body: `{ "notes": "need 5 people to harvest rice in Kenema" }`

Returns `{ "title", "description", "district", "workersNeeded", "skills" }`. Pay is left out so the AI never makes up a wage.

### POST /api/ai/worker-profile-draft

Body: `{ "experience": "string?", "skills": "string?", "district": "string?", "availability": "string?" }`. Send `experience` or `skills`, at least.

Returns `{ "headline", "bio", "district", "skills" }`.

### GET /api/ai/jobs/{id}/matches (Admin)

Returns up to 10 of `{ "workerUserId", "name", "headline", "district", "skills", "score", "reason" }`.

Workers score 3 for the same district and 2 for each shared skill. The AI writes the `reason`, with a plain reason used if it is unavailable. Phone numbers are not included.
