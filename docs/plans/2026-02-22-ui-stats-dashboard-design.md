# UI Stats Dashboard Design

**Date:** 2026-02-22
**Status:** Approved

## Problem

The current dashboard has four broken API calls:
- `POST /api/sync` — SyncController was deleted during cleanup
- `GET /api/bookings/provisional` — wrong path (correct: `/api/bookings?status=provisional`)
- `GET /api/comments/new` — endpoint never existed
- `GET /api/emails/unread` — endpoint never existed

The dashboard sections for "New Comments" and "Unread Emails" have no backing API and should be removed.

## Solution

Replace the current 3-pane broken dashboard with a clean stats dashboard showing at-a-glance booking activity, plus a working manual sync button.

## Backend

### New: `POST /api/sync`

Extracts the startup OSM sync logic from `Program.cs` into a `SyncService` (scoped), callable from both startup and on-demand. Returns:

```json
{ "bookingsCount": 1115, "syncedAt": "2026-02-22T10:30:00Z" }
```

### New: `GET /api/bookings/stats`

Single DB query returning four counts plus last sync time:

```json
{
  "onSiteNow": 3,
  "arrivingThisWeek": 7,
  "arrivingNext30Days": 22,
  "provisional": 14,
  "lastSynced": "2026-02-22T10:30:00Z"
}
```

- **onSiteNow**: `StartDate <= today <= EndDate` AND `Status == "confirmed"`
- **arrivingThisWeek**: `StartDate` within next 7 days
- **arrivingNext30Days**: `StartDate` within next 30 days
- **provisional**: `Status == "provisional"`
- **lastSynced**: `MAX(LastFetched)` from `OsmBookings`

## Frontend

### `apiClient.ts`

Remove broken `syncApi`, `commentsApi`, and incorrect `bookingsApi.getProvisional`.
Add:
- `syncApi.sync()` → `POST /api/sync`
- `bookingsApi.getStats()` → `GET /api/bookings/stats`

### `Dashboard.tsx`

Rewrite with:
- **Auth status indicator** — green/amber dot from `GET /api/auth/osm/status`
- **4 stat cards**: On site now | Arriving this week | Arriving next 30 days | Provisional
- **Sync button** — triggers `POST /api/sync`, shows last synced timestamp, spinner while in progress
- Loading and error states per card

Remove "Unread Emails", "New Comments", "Provisional Bookings" sections.

## Out of scope

- Booking list / detail view
- Re-auth UI (user navigates to `/api/auth/osm/login` manually)
- Email linking actions
