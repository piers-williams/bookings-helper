# Audit-trail comments on booking move actions

## Overview

The three existing move actions (`change-site`, `move-activity`, `move-dates` in
`BookingActionsController`) currently mutate a booking in OSM with no record of *why*
or *what specifically* changed, beyond OSM's own item history. This feature makes every
successful move action post a comment to the booking in OSM (reusing the comment
infrastructure that already exists via `OsmService.PostCommentAsync`), giving admins an
audit trail directly on the booking.

Each comment always contains an auto-generated summary of the change (e.g. `"Site
changed: Pitch 4 → Pitch 7."`). Users can optionally add a free-text note when
confirming the action, which is appended to the summary (e.g. `"... Note: customer
requested closer pitch."`).

This is separate from the existing general-purpose "Post Comment" box in the Comments
Timeline (`POST /api/bookings/{id}/comments`), which is unaffected.

## Backend changes

### Request DTOs

Add an optional `Note` field (string?) to:
- `MoveActivityRequest`
- `ChangeSiteRequest`
- `MoveDatesRequest`

Add an optional `NewSiteName` field (string?) to `ChangeSiteRequest` — the frontend
already has this from the available-sites dropdown it renders, and passing it avoids
an extra OSM lookup purely to build a sentence. If omitted, the summary falls back to
the raw site id.

### Summary composition

A private helper in `BookingActionsController` builds the auto-summary per action,
using data already available at the call site (the original `BookingItemDto` fetched
for validation, plus the request fields):

- **change-site**: `"Site changed: {originalItem.Label} → {NewSiteName ?? NewSiteId}."`
- **move-activity**: `"Moved '{originalItem.Label}': {old date/time} → {new date/time}."`
  — only the fields the request actually overrides are shown as changed.
- **move-dates**: `"Dates shifted by {DayShift} day(s)."`

If `Note` is non-empty, append `" Note: {Note}"` to the summary.

### Posting the comment

After `_mutationService.ReplaceItemsAsync(...)` returns, only if `result.Status` is
`Completed` or `CompletedWithWarnings` (the move actually took effect):

1. Call `_osmService.PostCommentAsync(booking.OsmBookingId, summary)`.
2. On success, persist the returned `CommentDto` into the `OsmComment` table — the same
   persistence logic already used by `BookingsController.PostComment` — so it appears
   immediately in the Comments Timeline without waiting for the next sync.
3. On failure (throws, or returns null):
   - Log a warning (do not throw).
   - If `result.Status` was `Completed`, downgrade it to `CompletedWithWarnings` and
     append `"; audit comment failed to post"` to `result.Message`.
   - If it was already `CompletedWithWarnings`, just append the same note.

`RolledBack` and `Failed` results skip commenting entirely — nothing happened to
summarize.

## Frontend changes

All changes are in `BookingsAssistant.Web/src/components/BookingDetail.tsx`.

- **Move-activity** and **change-site** confirm stages (the per-item "Are you sure?"
  block, reached after clicking "Move"/"Change") each gain a `<textarea>` labeled "Add
  a note (optional)", shown alongside the existing "Confirm"/"Cancel" buttons.
- **Move-dates** confirm stage gets the same textarea.
- New `note`/`setNote` state, reset whenever a new item's action form is opened or an
  action is cancelled (alongside the existing per-item form state reset).
- `handleMoveActivity`, `handleChangeSite`, `handleMoveDates` pass `note` through to
  `bookingsApi.moveActivity/changeSite/moveDates`. `handleChangeSite` also passes the
  selected site's `name` (from `availableSites`) as `newSiteName`.
- No changes to the result banner or Comments Timeline rendering — the existing
  refetch-after-action behavior will show the new comment automatically since it's
  persisted synchronously.

## Testing plan

- `BookingActionsTests.cs`: for each of the three actions —
  - successful move + successful comment post: assert an `OsmComment` row exists with
    the expected composed summary text (with and without a note).
  - successful move + comment-post failure (`FakeOsmService.PostCommentAsync` returns
    null / throws): assert status downgrades `Completed` → `CompletedWithWarnings` and
    the message mentions the comment failure.
  - rolled-back/failed move: assert `PostCommentAsync` is never called.
- Unit test the summary-composition helper directly for each action type, with and
  without a note.
- Frontend RTL tests (existing `BookingDetail` test file): the note textarea appears
  only at the confirm stage, its value is passed through to the API client call, and it
  clears when switching items or cancelling.
