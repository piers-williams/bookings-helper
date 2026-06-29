# OSM Booking-Item Contract (captured fixtures)

Captured from a live OSM web session (HAR, 2026-06-29) against test booking **179743**
("Test - Piers", campsite 219 Thorrington). PII redacted (`group_name` → `REDACTED`); all
IDs, dates, and field names preserved. These are the fixtures for wiring the deferred
`OsmService` item seams (issue #27 / `osm-payload-mapping` chunk of PR #25).

> **Auth caveat:** the HAR write calls used the browser **session cookie**, not our OAuth
> Bearer token. URLs/methods/payloads are confirmed; the OAuth `section:campsite_bookings:write`
> scope coverage for `addItem`/`delete` is **NOT** confirmed by this capture — verify with a
> live token smoke test.

## Endpoints

### 1. List booked items
**Booked items live in the booking-detail response, NOT the items catalogue.**
- `GET /v3/campsites/bookings/{bookingId}` → `data.items[]`  ← parse this for `GetBookingItemsAsync`
- `GET /v3/campsites/{campsiteId}/items?booking_id={id}&mode=booking&audience=venue` returns the
  **catalogue tree** of bookable item-types (categories via `parent_id`), with empty `bookings[]`
  for booked items in this capture. See `items-catalogue-list.json`. (PR #25 pointed
  `GetBookingItemsAsync` at this URL — that returns the catalogue, not the booked items. Correction needed.)

Fixture: `booking-detail-with-items.json` (one site item + one activity item).

**Booked-item shape (`data.items[]`):**
| Field | Example (site / activity) | Notes |
|-------|---------------------------|-------|
| `id` | 411467 / 411468 | **booked-item id** → used for delete |
| `campsite_item_id` | 1387 / 4961 | **item-type id** → used in addItem URL |
| `campsite_booking_id` | 179743 | parent booking |
| `start_timestamp` / `end_timestamp` | "2027-12-04 00:01:00" | full datetime (date + time) |
| `number_people` | 20 / 10 | |
| `number_sessions` | 1 | |
| `price_per_person`/`price_per_session` | "0.00" / "50.00" | strings |
| `flexible_times` / `flexible_price_mode` | true / null·"hourly" | |
| `number_instructors_required` | 0 / 1 | **>0 ⇒ activity** |
| `campsite_instructor_type_id` | 0 / 422 | **≠0 ⇒ activity** |
| `item` | nested catalogue node | `item.parent_id` chains to "Campsites"/"Indoor Accommodation" (site) vs "Activities" (activity); activity `name` is prefixed `ACTIVITY - ` |
| `booking_questions[]` | 3 / 1 | per-item Q&A, added after create |

**Site vs activity** = one item *type* with optional fields (not distinct schemas). Discriminate by
`campsite_instructor_type_id != 0` / `number_instructors_required > 0`, or by `item.parent_id` lineage,
or the `ACTIVITY - ` name prefix.

### 2. Create (add) a booked item
- `POST /v3/campsites/bookings/{bookingId}/addItem/{campsiteItemId}`
- `Content-Type: application/x-www-form-urlencoded`
- Body: `slot_id`, `start` (yyyy-MM-dd), `end` (yyyy-MM-dd), `number_people`, `start_time` (HH:mm), `end_time` (HH:mm)
- Response: `{"status":true,"error":null,"data":{"id":<newBookedItemId>,"item_name":"...","questions":<n>},"meta":[]}`

Fixtures: `create-item-1387.json` (site), `create-item-4961.json` (activity). Same payload shape for both.

**`slot_id` dependency (important for clone/move):** `slot_id` is **not** a field on the booked item —
it comes from the availability endpoint:
- `GET /v3/campsites/items/{campsiteItemId}/availability?booking_id={bookingId}` → `data[]`, each with
  `id` (= the slot_id), `start`, `end`, `start_time`, `end_time`, `multi_day`, `available`, `cost`.
- To clone to a **new date/site**, call availability for the target item-type and select the slot whose
  start/end match the desired window. A same-window clone can reuse the original's slot only if you
  re-derive it from availability (the booked item doesn't store slot_id).

Fixtures: `availability-1387.json` (site: slot 8279 = multi-day 12-04→12-05), `availability-4961.json`
(activity: slot 9235 = 12-05 09:00–21:00).

### 3. Delete a booked item
- `POST /v3/campsites/bookings/items/{bookedItemId}/delete` (POST-with-action, not HTTP DELETE)
- Empty body. Response: `{"status":true,"error":null,"data":[],"meta":[]}`

Fixtures: `delete-item-411467.json`, `delete-item-411468.json`.

### 4. Booking questions (in scope — answers ARE replayed on clone)
`addItem` auto-creates blank question placeholders (`response.data.questions` = count).
- `GET /v3/campsites/bookings/items/{bookedItemId}/questions` → `data.questions[]`, each with `id`
  (answer-row id, **per-item — differs between original and clone**), `campsite_booking_question_id`
  (**stable question-definition id — match on this**), `question`, `answer`.
- `POST /v3/campsites/bookings/items/{bookedItemId}/questions`, form field
  `answers=<JSON array>` of `[{"id":<answerRowId>,"answer":"..."}]` → `{"status":true,...}`.

Fixtures: `questions-get-411467.json`, `questions-post-411467.json` (site, 3 Qs), `questions-get-411468.json`,
`questions-post-411468.json` (activity, 1 Q). `raw_body` in the post fixtures shows the URL-encoded form;
`decoded_answers` shows the parsed JSON array.

**Replay algorithm (clone):**
1. Read original answers from the booking-detail item's `booking_questions[]` (already fetched) — keyed by
   `campsite_booking_question_id` → `answer`.
2. After creating the clone, `GET` the clone's `/questions` to learn its NEW answer-row `id`s.
3. For each clone question, look up the original answer by `campsite_booking_question_id`, build
   `[{"id": cloneRowId, "answer": originalAnswer}]`, and `POST` to the clone's `/questions`.
   (Row `id`s differ per item, so matching MUST be on `campsite_booking_question_id`, not `id`.)

## Open questions from #26 — answered
- ✅ Item field set — see table above.
- ✅ Site vs activity — one type, discriminated by instructor fields / parent lineage / name prefix.
- ✅ Create endpoint URL/method/payload — `POST addItem/{typeId}`, form-encoded, needs `slot_id` from availability.
- ✅ Delete endpoint — `POST items/{bookedItemId}/delete`.
- ⚠️ OAuth scope — **unconfirmed** (HAR used cookies). Needs live Bearer-token smoke test.
- ➕ New finding: `slot_id` requires an availability lookup; cloning to a new window is not a pure field copy.
