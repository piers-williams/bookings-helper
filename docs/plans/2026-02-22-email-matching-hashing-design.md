# Email Matching & Privacy Hashing Design

**Date:** 2026-02-22
**Status:** Approved

## Problem

Current email-to-booking matching only works via booking reference regex (e.g. `#12345`). The email-address fallback never fires because `CustomerEmail` is null for all bookings (OSM list API doesn't return it). There is no group name matching. Email addresses are stored in plaintext.

## Goals

1. Match emails to bookings via group/organisation name when no booking ref is present
2. Match emails to bookings via customer email address (once backfilled from OSM detail API)
3. Store no PII in plaintext — hash all email addresses and use hashes for matching

## Hashing Algorithm

**PBKDF2-SHA256** with the server secret used as the salt:

```
PBKDF2-SHA256(
  password   = value.ToLowerInvariant().Trim(),
  salt       = serverSecret,          // 32 random bytes from /data/hash-secret.txt
  iterations = 200,000,
  keyLength  = 32 bytes
) → lowercase hex string (64 chars)
```

The server secret is auto-generated on first startup and persisted to `/data/hash-secret.txt`. It survives container updates alongside the SQLite DB. Using the secret as the salt means an attacker with only the DB cannot compute hashes.

A single `HashingService` singleton method `HashValue(string value)` is used for all hashing (emails and names).

## Data Model Changes

### `EmailMessages` table

| Before | After |
|---|---|
| `SenderEmail varchar(255)` | `SenderEmailHash varchar(64)` |
| `SenderName varchar(255)` | `SenderName varchar(255)` (unchanged) |

Migration: hash existing `SenderEmail` values → `SenderEmailHash`, then drop `SenderEmail`.

Duplicate detection changes from `SenderEmail + Subject + ReceivedDate` to `SenderEmailHash + Subject + ReceivedDate`.

### `OsmBookings` table

| Column | Change |
|---|---|
| `CustomerEmail` (always null) | Replaced by `CustomerEmailHash varchar(64)` (nullable) |
| `CustomerName` (plaintext) | Kept as-is for UI display |
| `CustomerNameHash` (new) | `varchar(64)` nullable — PBKDF2 of `CustomerName` |

`CustomerName` stays in the DB for display purposes. `CustomerNameHash` is derived from it and used for matching only.

## Matching Pipeline

Triggered on every email capture (`POST /api/emails/capture`). Runs in priority order:

1. **Booking ref** (existing, unchanged) — regex extracts refs from subject + body → **auto-link** created immediately
2. **Email hash** — `PBKDF2(senderEmail) == CustomerEmailHash` → returned as `suggestedBookings`
3. **Name hash** — any `PBKDF2(candidateName) == CustomerNameHash` → returned as `suggestedBookings`

Steps 2 and 3 are suggestions only — the user confirms before a link is created. Both run regardless of whether step 1 found a match.

## Capture Request Changes

`CaptureEmailRequest` gains:

```json
{
  "senderEmail": "john@example.com",
  "senderName": "John Smith",
  "subject": "...",
  "bodyText": "...",
  "receivedDate": "...",
  "candidateNames": ["John Smith", "1st Scouts UK", "Anytown Primary School"]
}
```

`candidateNames` — plaintext names extracted by the extension (see below). The backend hashes each before comparing. The extension does no crypto.

## Backfill Worker

`BookingDetailBackfillService : BackgroundService`

- Runs every 30 minutes
- Queries: `CustomerEmailHash IS NULL AND Status NOT IN ('Past', 'Cancelled')`, batch of 20
- For each booking: calls `IOsmService.GetBookingDetailsAsync(osmBookingId)`
- Parses customer email from the JSON response (logs and skips gracefully if field absent)
- Hashes email → stores `CustomerEmailHash`
- Also populates `CustomerNameHash` from `CustomerName` if not already set
- Registered in `Program.cs` via `builder.Services.AddHostedService<BookingDetailBackfillService>()`

## Chrome Extension Changes

**Name extraction** — before sending the capture request, the extension:

1. Adds the sender display name as a candidate (already available from the From header)
2. Scans the email body for sign-off phrases: `Regards`, `Kind regards`, `Best regards`, `Best wishes`, `Thanks`, `Many thanks`, `Cheers`, `Yours`, `Sincerely`
3. Collects non-empty lines immediately following the sign-off (up to 3 lines) as additional candidates
4. Deduplicates and sends all as `candidateNames[]`

No crypto code is added to the extension — all hashing happens on the backend.

## Out of Scope

- Fuzzy / word-overlap name matching
- Displaying matched email addresses in the UI (hashes only, never shown)
- Fetching `CustomerEmail` for Past/Cancelled bookings
- Manual backfill trigger in the UI
