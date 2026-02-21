# OSM Booking Sync Design

**Date:** 2026-02-21
**Status:** Approved for Implementation

## Context

`GET /api/bookings` fetches live bookings from OSM via `OsmService` but discards them after returning the response. The `OsmBookings` table therefore stays empty (only seeded mock data), so `LinkingService` never finds a match when an email references a real booking ref like `#12345`.

## Goal

Populate `OsmBookings` from the OSM API so that email→booking matching works with real data.

## Approach

Two changes to `BookingsController` only — no new service class.

### 1. Write-through on `GET /api/bookings`

After `OsmService.GetBookingsAsync` returns successfully, upsert each booking into `OsmBookings` before returning the response. The caller sees no change; the DB update is a transparent side effect.

### 2. `POST /api/bookings/sync`

New endpoint that fetches "provisional" and "confirmed" statuses from OSM in parallel and upserts both. Returns a summary: `{ added, updated, total }`. Decorated with `[EnableCors("ExtensionCapture")]` for future extension use.

### Shared upsert helper

Both paths call a private `UpsertBookingsAsync(List<BookingDto> bookings)` method on the controller that:
- For each booking, checks `OsmBookings` by `OsmBookingId`
- If found: updates `CustomerName`, `StartDate`, `EndDate`, `Status`, `LastFetched`
- If not found: inserts new `OsmBooking`
- Calls `SaveChangesAsync()` once after processing all bookings

### `CustomerEmail`

The OSM bookings list API returns `group_name` but not an email address. `CustomerEmail` stays null for now. Booking-ref matching (`#12345` in subject) works without it. Sender-email-based suggestions will remain empty until per-booking detail fetching is added in a future phase.

## Data Flow

### After this change — opening an email in OWA

1. User opens email: *"Re: Booking #12345 – deposit query"*
2. Extension → `POST /api/emails/capture`
3. `LinkingService` extracts `"12345"` from subject
4. Queries `OsmBookings` where `OsmBookingId == "12345"` → **found** (synced earlier)
5. Creates `ApplicationLink`
6. Returns booking card to sidebar ✓

### Sync trigger options

| Trigger | How |
|---------|-----|
| Dashboard page load | `GET /api/bookings` write-through |
| Manual full refresh | `POST /api/bookings/sync` |
| Future: extension on startup | `POST /api/bookings/sync` with CORS |

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| OSM unreachable during `GET /api/bookings` | `GetBookingsAsync` returns empty list, upsert skipped, response is empty list (existing behaviour) |
| OSM unreachable during `POST /api/bookings/sync` | Returns 502 with error message |
| Token expired | `OsmAuthService` throws, controller returns 401 (existing behaviour) |
| Partial fetch (one status fails) | Sync endpoint continues with whichever statuses succeeded |

## Files Changed

- `BookingsAssistant.Api/Controllers/BookingsController.cs` — add upsert helper + write-through + sync endpoint

## Non-Goals

- Fetching `CustomerEmail` per booking (N+1 API calls — future phase)
- Background scheduled sync (no need yet)
- Syncing comments on bulk sync (only on individual booking detail fetch)
