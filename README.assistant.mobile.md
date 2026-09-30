# AI Assistant & My Farms — Mobile App Integration & Design Guide (Flutter)

Audience: the designer and Flutter developer building the **AgriSmart AI Assistant** chat and the **My Farms** screens in AgriSmart-UI.
General app conventions (base URLs, login, tokens): [README.mobile.md](README.mobile.md).

Base URLs (local dev): `http://localhost:5150` · `https://localhost:7004`. Try every call live at `/swagger` or `/scalar/v1` (tags **AI assistant** and **My farms**).

## What the features do

**My Farms**: after logging in, the farmer saves their farms: name, district, GPS location, size, and the crops they grow (with planting dates and plot sizes). They can edit or delete them later.

**AI Assistant**: the farmer types a question (or speaks it; the phone turns speech into text) such as *"When should I plant rice?"*, *"What is wrong with my cassava?"* or *"How much fertilizer do I need?"*. The assistant answers in short, simple sentences, **using the farmer's saved farms**, the weather forecast, the fertilizer calculator and the harvest predictor. It only answers farming questions.

The two features are linked: **the more farm data the farmer saves, the better the answers.** Without a saved farm, the assistant asks the farmer for details such as crop, size and location.

## Before you start

* Every endpoint here needs the login token, **except** `GET /api/assistant/status`. Send `Authorization: Bearer <token>`.
* Tokens expire after **10 minutes** and there is no refresh endpoint. On `401`, send the user to login, then retry. **Keep the chat on screen and keep the `conversationId`** so the farmer does not lose the conversation.
* JSON is camelCase, and enums are camelCase strings (`"acre"`, `"squareMeter"`).
* Unlike the fertilizer endpoints, farm responses **can contain `null`** for optional fields the farmer left empty (`district`, `latitude`, `longitude`, `areaHectares`, `plantingDate`, `plotSizeHectares`). Handle nulls.
* Routes: farms are under `/farms`, the assistant is under `/api/assistant`.

---

# Part 1 — My Farms (`/farms`)

## Suggested screens & flow

```
[Login] → (no farms yet?) → [Add your farm] → [My farms list] → [Farm details / Edit] → (Delete)
```

* After login, call `GET /farms`. If the list is empty, show a friendly prompt: *"Add your farm so the assistant can give you better advice."* Don't block the app; adding a farm is optional.
* Also offer "Add farm" from the assistant screen (see Part 2).

## Endpoints

| Method | Route | What it does | Success |
|---|---|---|---|
| `GET` | `/farms` | The logged-in farmer's farms, oldest first | `200` list |
| `POST` | `/farms` | Add a farm, optionally with crops | `201` farm |
| `PUT` | `/farms/{id}` | Edit a farm | `200` farm |
| `DELETE` | `/farms/{id}` | Delete a farm and its crops | `204` empty |

A farmer only ever sees and changes **their own** farms. Another farmer's farm id returns `404`.

## The farm object (response)

```json
{
  "id": "3f2c9a1e-7b1d-4c55-9f3e-2a6d8c0b1e44",
  "name": "River farm",
  "district": "Bo",
  "latitude": 7.96,
  "longitude": -11.74,
  "areaHectares": 0.8094,
  "crops": [
    { "id": "a1b2c3d4-0000-0000-0000-000000000001", "cropId": "maize",   "plantingDate": "2026-07-01", "plotSizeHectares": 0.5 },
    { "id": "a1b2c3d4-0000-0000-0000-000000000002", "cropId": "cassava", "plantingDate": null,         "plotSizeHectares": null }
  ],
  "createdAt": "2026-09-30T18:02:11.123+00:00"
}
```

* Sizes always come back in **hectares** (`areaHectares`, `plotSizeHectares`), whatever unit the farmer typed. Convert for display if the farmer prefers acres (1 acre = 0.4047 ha).
* `cropId` is a key. Show the crop's display name from `GET /crops` (and/or `GET /fertilizer/crops`).

## Add a farm — `POST /farms`

```json
{
  "name": "River farm",
  "district": "Bo",
  "latitude": 7.96,
  "longitude": -11.74,
  "area": 2,
  "areaUnit": "acre",
  "crops": [
    { "cropId": "maize", "plantingDate": "2026-07-01", "plotSize": 0.5, "areaUnit": "hectare" },
    { "cropId": "cassava" }
  ]
}
```

| Field | Required | Rules | UI suggestion |
|---|---|---|---|
| `name` | **yes** | 1–100 characters | Text field, e.g. "River farm", "Farm near the school" |
| `district` | no | One of Sierra Leone's 16 districts (see below); any capitalisation | Dropdown |
| `latitude`, `longitude` | no | **Both or neither**; lat −90..90, lon −180..180 | "Use my current location" button (GPS) while standing on the farm. Don't make the farmer type numbers. |
| `area` | no | > 0, at most 10,000 ha after conversion | Numeric keypad |
| `areaUnit` | no | `"hectare"` (default), `"acre"`, `"squareMeter"` | Segmented control; **acre** is usually most familiar |
| `crops` | no | List of crops (below) | "Add crop" rows |

Each crop:

| Field | Required | Rules | UI suggestion |
|---|---|---|---|
| `cropId` | **yes** | A key from `GET /crops` or `GET /fertilizer/crops` | Crop picker with icons |
| `plantingDate` | no | `yyyy-MM-dd`, within the last 5 years and at most 1 year ahead | Date picker. **Encourage it**: the assistant needs it for harvest dates. |
| `plotSize` | no | > 0 | Numeric keypad |
| `areaUnit` | no | `"hectare"` (default), `"acre"`, `"squareMeter"` | Same control as the farm size |

**Districts** (send exactly one of these; the server fixes capitalisation):
`Bo`, `Bombali`, `Bonthe`, `Falaba`, `Kailahun`, `Kambia`, `Karene`, `Kenema`, `Koinadugu`, `Kono`, `Moyamba`, `Port Loko`, `Pujehun`, `Tonkolili`, `Western Area Rural`, `Western Area Urban`.

**Crop picker**: there are two crop lists and a farm crop may come from either. Merge `GET /crops` (harvest prediction crops, e.g. `maize`, `rice-lowland`, `rice-upland`, `cassava`, `groundnut`, `pepper-chili`) and `GET /fertilizer/crops` (fertilizer crops, which also include e.g. cocoa, oil palm and okra) by `id`, and cache the result. **Prefer the `GET /crops` id when both lists have the crop**, because harvest prediction only works with those ids.

Why each field matters to the assistant:

| Saved data | The assistant uses it for |
|---|---|
| GPS location | Weather forecast, harvest date prediction |
| Crop + planting date | Harvest date prediction, "what stage is my crop at" |
| Plot size per crop | Fertilizer amounts ("how many bags do I need") |
| District | Local context in answers |

## Edit a farm — `PUT /farms/{id}`

Same body and rules as `POST /farms`. Returns `200` with the updated farm.

* **Send the whole farm.** Name, district, location and area are replaced, so a field you leave out is **cleared**. Pre-fill the edit form from the farm object and send everything back.
* **Crops:**
  * Leave `crops` out (or send `null`): the farm's crops are **kept unchanged**.
  * Send a list: the farm's crops are **replaced** by that list. `[]` removes all crops.
  * So to add, change or remove one crop, send the full new crop list.
* Sizes in the response are in hectares. When pre-filling the form, either show hectares or convert back to the farmer's unit.

## Delete a farm — `DELETE /farms/{id}`

Returns `204` with an empty body. The farm and its crops are gone. Ask for confirmation first (*"Delete River farm? This cannot be undone."*).

## Farm errors

| Status | When | Body |
|---|---|---|
| `400` | Invalid input | `{ "message": "Validation failed: ...", "errors": { "name": ["name is required, up to 100 characters."], "crops[0].cropId": ["..."] } }` |
| `400` | Unknown enum (e.g. `"areaUnit": "furlong"`), bad date format, or malformed JSON | ASP.NET `ValidationProblemDetails`, whose `errors` keys look like `"$.areaUnit"` |
| `401` | Missing or expired token | empty |
| `404` | Farm not found, or it belongs to someone else | `{ "message": "Farm '…' was not found." }` |

`errors` keys match the request JSON: `name`, `district`, `latitude`, `longitude`, `area`, `areaUnit`, `crops[0].cropId`, `crops[0].plantingDate`, `crops[0].plotSize`, `crops[0].areaUnit`. Show them as inline field errors, and use `message` as a fallback snackbar.

Client-side checks worth mirroring (the server checks them anyway): name required; latitude and longitude together; sizes > 0; district from the list.

---

# Part 2 — AI Assistant (`/api/assistant`)

## Suggested screens & flow

```
[Home] → (status enabled?) → [Assistant chat] ⇄ questions/answers → (long-press reply) → [Report reply]
```

* On app start (or when opening the tab), call `GET /api/assistant/status`. Hide the Assistant entry point if `enabled` is `false`.
* A normal chat screen: farmer bubbles on the right, assistant bubbles on the left, a text box and **a microphone button** (speech-to-text on the phone, sending the text).
* If `GET /farms` is empty, show a banner in the chat: *"Add your farm for better advice"* → Add farm screen.

## 1. Is the assistant on? — `GET /api/assistant/status` (no login)

```json
{ "enabled": true }
```

When `false`, the chat and report endpoints return `404`. Hide or disable the feature.

## 2. Ask a question — `POST /api/assistant/chat` (login required)

Request:

```json
{ "conversationId": null, "message": "When should I plant rice?" }
```

| Field | Rules |
|---|---|
| `conversationId` | `null` (or omitted) to **start** a new conversation; the id from the previous reply to **continue** it |
| `message` | **Required**, 1–1000 characters (after trimming spaces) |

Response `200`:

```json
{
  "conversationId": "9d4f5c2e-1a7b-4e0f-8c3d-6b2a1f0e9d88",
  "messageId": "c7e1b3a2-5d4f-4a6b-9e8c-1f2d3a4b5c6d",
  "reply": "Plant upland rice when the rains are steady, usually from May to June. ..."
}
```

* **Keep `conversationId`** and send it with the next question, so the assistant remembers the conversation (it remembers roughly the last 20 messages).
* **Keep `messageId` with each reply bubble**. You need it to report that reply.
* To start a fresh topic, send `conversationId: null` ("New chat" button).
* A conversation belongs to the farmer who started it. Using another farmer's `conversationId` returns `404`. If you get `404` for a saved conversation id, start a new conversation.

**There is no endpoint to list past conversations or messages.** Store the chat history on the phone (e.g. local database per `conversationId`) if you want to show it after the app restarts.

### Replies are slow — design for it

A reply can take **several seconds, sometimes 20–30 s**: the assistant may look up the farm, weather, fertilizer calculator and harvest prediction before answering.

* Show a "typing…" indicator in an assistant bubble right away.
* Set the HTTP timeout to at least **90 seconds** for this call.
* Disable the send button while waiting (one question at a time per conversation).
* Show the farmer's own message immediately (optimistic), and mark it "not sent – tap to retry" if the call fails.

### Showing the reply

* `reply` is plain text in short sentences. It may contain line breaks and simple lists ("- ..."). Render line breaks; treat it as plain text, not HTML.
* The assistant replies in the **same language the farmer writes in**, so the UI needs no language switch for this.
* Under each assistant bubble, show a small note or info icon: *"AI answers can be wrong. For serious crop problems, talk to your agricultural extension officer."*
* Numbers from the fertilizer calculator and harvest predictor come from the same engines as those screens. Offer shortcuts ("Open fertilizer calculator", "Open harvest prediction") under the chat if useful.

### What the assistant will and won't do (useful for copy and onboarding)

* ✅ Planting times, crop care, fertilizer amounts, harvest dates, weather for farming, likely causes of crop problems.
* ✅ Uses the farmer's saved farms. If data is missing, it asks.
* ❌ Won't answer non-farming questions (politely declines).
* ❌ Won't recommend pesticide or chemical doses; it points the farmer to an extension officer.
* ❌ No rainfall forecast (only temperatures are available).
* ❌ Photos aren't supported yet; text only. (Photo questions are planned; the request will get an optional extra field later and existing app versions will keep working.)

Example starter chips to show on an empty chat:
*"When should I plant rice?"* · *"How much fertilizer for my maize?"* · *"When will my cassava be ready?"* · *"My leaves are turning yellow"*

## 3. Report a reply — `POST /api/assistant/report` (login required)

**Google Play requires** that users can flag AI-generated content. Add a way to report **every assistant reply**, e.g. a flag icon or long-press → "Report this answer".

Request:

```json
{
  "conversationId": "9d4f5c2e-1a7b-4e0f-8c3d-6b2a1f0e9d88",
  "messageId": "c7e1b3a2-5d4f-4a6b-9e8c-1f2d3a4b5c6d",
  "reason": "The advice is wrong"
}
```

| Field | Rules |
|---|---|
| `conversationId`, `messageId` | From the chat response of the reply being reported |
| `reason` | **Required**, 1–1000 characters |

Response: `204`, empty body. Show *"Thanks, we'll review this answer."*

Suggested reason picker (send the chosen text, plus any comment the farmer types):
*"Wrong or dangerous advice"* · *"Not about farming"* · *"Offensive"* · *"Other"*

Only the farmer's own **assistant** replies can be reported; anything else returns `404`.

## Assistant errors

| Status | When | Body | What to show |
|---|---|---|---|
| `400` | `message` empty or over 1000 characters; `reason` empty or too long | `{ "message": "message is required.", "errors": { "message": ["message is required."] } }` | Inline error under the text box |
| `401` | Missing or expired token | empty | Login, then retry |
| `404` | Assistant switched off; or conversation/message not found / not the farmer's | `{ "enabled": false }` when switched off, otherwise `{ "message": "..." }` | Hide the feature, or start a new conversation |
| `429` | Daily limit reached (**30 questions/reports per farmer per day**) | `{ "message": "You have reached today's limit for the assistant. Please try again tomorrow." }` | Show the message; disable send until tomorrow |
| `503` | The AI service is down or failed | `{ "message": "The assistant is unavailable right now. Please try again later." }` | Show the message with a "Try again" button; keep the farmer's question in the box |

A failed question (`429`, `503`, network error) is **not saved**. Retrying the same question is safe.

---

## Worked example (curl)

```bash
# 1. Log in
TOKEN=$(curl -s -X POST http://localhost:5150/auth/login -H "Content-Type: application/json" \
  -d '{"email":"farmer@example.com","password":"secret123"}' | jq -r .token)

# 2. Add a farm
curl -X POST http://localhost:5150/farms -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"River farm","district":"Bo","latitude":7.96,"longitude":-11.74,"area":2,"areaUnit":"acre",
       "crops":[{"cropId":"maize","plantingDate":"2026-07-01","plotSize":0.5}]}'

# 3. List farms
curl http://localhost:5150/farms -H "Authorization: Bearer $TOKEN"

# 4. Edit it (whole farm; crops omitted = kept)
curl -X PUT http://localhost:5150/farms/<farmId> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"River farm","district":"Bo","latitude":7.96,"longitude":-11.74,"area":3,"areaUnit":"acre"}'

# 5. Is the assistant on?
curl http://localhost:5150/api/assistant/status

# 6. Ask a question (new conversation)
curl -X POST http://localhost:5150/api/assistant/chat -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"conversationId":null,"message":"When will my maize be ready to harvest?"}'
# -> { "conversationId": "...", "messageId": "...", "reply": "..." }

# 7. Follow-up in the same conversation
curl -X POST http://localhost:5150/api/assistant/chat -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"conversationId":"<conversationId>","message":"How much fertilizer should I add now?"}'

# 8. Report a reply
curl -i -X POST http://localhost:5150/api/assistant/report -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"conversationId":"<conversationId>","messageId":"<messageId>","reason":"Wrong or dangerous advice"}'

# 9. Delete the farm
curl -i -X DELETE http://localhost:5150/farms/<farmId> -H "Authorization: Bearer $TOKEN"
```

## Design & copy notes

* **Language:** plain words, short sentences, big text. Many users read little, so use icons for crops, the microphone, GPS and the report flag.
* **Voice first:** make the microphone button as prominent as the text box. Let the farmer edit the transcribed text before sending.
* **Location:** "Use my location" should be one tap. Explain why: *"So we can check the weather at your farm."*
* **Trust:** keep the "AI answers can be wrong" note and the report option visible on every reply.
* **Offline:** the assistant needs the network. Show a clear offline state instead of a spinner. Cache the farm list and crop lists for the My Farms screens.
* **Accessibility:** touch targets ≥ 48 dp, support large text and screen readers (announce new assistant replies).

## Checklist

- [ ] `GET /farms` after login; prompt to add a farm when empty
- [ ] Add farm form (GPS button, district dropdown, size + unit, crops with planting date and plot size)
- [ ] Farm list, edit (send whole farm; crops list replaces), delete with confirmation
- [ ] Handle nulls in farm responses; sizes returned in hectares
- [ ] `GET /api/assistant/status` gates the Assistant entry point
- [ ] Chat screen with typing indicator, ≥ 90 s timeout, one question at a time
- [ ] Keep `conversationId` for follow-ups; "New chat" sends `null`
- [ ] Store chat history on the phone (no history endpoint)
- [ ] Report button on every assistant reply (Google Play requirement), keeping `messageId` per bubble
- [ ] Handle `400`, `401` (re-login, keep chat), `404`, `429`, `503`
- [ ] "AI answers can be wrong" disclaimer
