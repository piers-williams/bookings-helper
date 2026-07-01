# Audit-Trail Comments on Booking Move Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `change-site`, `move-activity`, and `move-dates` post an audit-trail comment to OSM whenever they succeed, combining an auto-generated summary of the change with an optional user-typed note.

**Architecture:** A new static `BookingActionCommentComposer` builds the summary text per action type. `BookingActionsController` calls it after a successful `ReplaceItemsAsync`, posts the result via the already-existing `IOsmService.PostCommentAsync`, and persists the comment locally exactly like the existing `POST /api/bookings/{id}/comments` endpoint does. The frontend adds an optional note textarea to each action's confirm step and threads it through to the request.

**Tech Stack:** ASP.NET Core 8 / EF Core (backend), React + TypeScript + Vite (frontend), xUnit + `WebApplicationFactory` (backend tests), Vitest + React Testing Library (frontend tests).

## Global Constraints

- Do not add any new OSM API calls — reuse `IOsmService.PostCommentAsync`, which already posts to `/v3/comments/campsite_booking/{id}/add`.
- Only post the audit comment when the move result status is `Completed` or `CompletedWithWarnings`. `RolledBack` and `Failed` results must never trigger a comment post.
- If posting the audit comment fails (throws or returns null), downgrade a `Completed` result to `CompletedWithWarnings` and append `"; audit comment failed to post"` to `result.Message`. Never fail the HTTP request because of a comment-post failure.
- The optional `Note` / `note` field is omitted (not an empty string) from requests/DTOs when the user leaves it blank.
- Frontend: no changes to `apiClient.ts` are needed — its methods already forward the request object as-is.
- This worktree has no `node_modules` yet for `BookingsAssistant.Web`. Frontend steps must run `npm install` once via the Docker container before running any `npm test`.

---

## File Structure

- **Create** `BookingsAssistant.Api/Services/BookingActionCommentComposer.cs` — static helper building the three summary strings.
- **Create** `BookingsAssistant.Tests/Services/BookingActionCommentComposerTests.cs` — unit tests for the composer.
- **Modify** `BookingsAssistant.Api/Models/MoveActivityRequest.cs`, `ChangeSiteRequest.cs`, `MoveDatesRequest.cs` — add `Note` (and `NewSiteName` on `ChangeSiteRequest`).
- **Modify** `BookingsAssistant.Api/Controllers/BookingActionsController.cs` — call the composer and post/persist the audit comment after each successful mutation.
- **Modify** `BookingsAssistant.Tests/Controllers/BookingActionsTests.cs` — integration tests for the new comment-posting behavior.
- **Modify** `BookingsAssistant.Web/src/types/index.ts` — add `note`/`newSiteName` fields to the three request interfaces.
- **Modify** `BookingsAssistant.Web/src/components/BookingDetail.tsx` — add the note textarea to each confirm step and thread it through the three handlers.
- **Create** `BookingsAssistant.Web/src/components/BookingDetail.auditNote.test.tsx` — RTL tests for the note field.

---

### Task 1: `BookingActionCommentComposer` + request DTO fields

**Files:**
- Modify: `BookingsAssistant.Api/Models/MoveActivityRequest.cs`
- Modify: `BookingsAssistant.Api/Models/ChangeSiteRequest.cs`
- Modify: `BookingsAssistant.Api/Models/MoveDatesRequest.cs`
- Create: `BookingsAssistant.Api/Services/BookingActionCommentComposer.cs`
- Test: `BookingsAssistant.Tests/Services/BookingActionCommentComposerTests.cs`

**Interfaces:**
- Produces (used by Task 2): `BookingActionCommentComposer.ComposeChangeSiteSummary(BookingItemDto original, ChangeSiteRequest request) -> string`, `ComposeMoveActivitySummary(BookingItemDto original, MoveActivityRequest request) -> string`, `ComposeMoveDatesSummary(MoveDatesRequest request) -> string`.
- Produces (used by Task 2 and Task 3): `MoveActivityRequest.Note`, `ChangeSiteRequest.Note`, `ChangeSiteRequest.NewSiteName`, `MoveDatesRequest.Note` — all `string?`.

- [ ] **Step 1: Add the `Note`/`NewSiteName` fields to the request DTOs**

`BookingsAssistant.Api/Models/MoveActivityRequest.cs`:
```csharp
namespace BookingsAssistant.Api.Models;

public class MoveActivityRequest
{
    public string ItemId { get; set; } = string.Empty;
    public DateTime? NewStartDate { get; set; }
    public string? NewStartTime { get; set; }
    public string? NewEndTime { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
```

`BookingsAssistant.Api/Models/ChangeSiteRequest.cs`:
```csharp
namespace BookingsAssistant.Api.Models;

public class ChangeSiteRequest
{
    public string ItemId { get; set; } = string.Empty;
    public string NewSiteId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the target site, as already shown in the frontend's available-sites
    /// dropdown. Used to build a readable audit comment; falls back to NewSiteId if omitted.
    /// </summary>
    public string? NewSiteName { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
```

`BookingsAssistant.Api/Models/MoveDatesRequest.cs`:
```csharp
namespace BookingsAssistant.Api.Models;

public class MoveDatesRequest
{
    public int DayShift { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
```

- [ ] **Step 2: Write the failing unit tests for the composer**

Create `BookingsAssistant.Tests/Services/BookingActionCommentComposerTests.cs`:
```csharp
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Xunit;

namespace BookingsAssistant.Tests.Services;

public class BookingActionCommentComposerTests
{
    private static BookingItemDto MakeSiteItem() => new()
    {
        ItemId = "site-item-1",
        Type = "site",
        SiteId = "site-42",
        Label = "Pitch 4"
    };

    private static BookingItemDto MakeActivityItem() => new()
    {
        ItemId = "act-item-1",
        Type = "activity",
        ActivityId = "act-10",
        Label = "Archery Session",
        StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
        StartTime = "10:00",
        EndTime = "12:00"
    };

    [Fact]
    public void ComposeChangeSiteSummary_UsesNewSiteName_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(
            MakeSiteItem(),
            new ChangeSiteRequest { ItemId = "site-item-1", NewSiteId = "site-99", NewSiteName = "Pitch 7" });

        Assert.Equal("Site changed: Pitch 4 → Pitch 7.", summary);
    }

    [Fact]
    public void ComposeChangeSiteSummary_FallsBackToSiteId_WhenNewSiteNameMissing()
    {
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(
            MakeSiteItem(),
            new ChangeSiteRequest { ItemId = "site-item-1", NewSiteId = "site-99" });

        Assert.Equal("Site changed: Pitch 4 → site-99.", summary);
    }

    [Fact]
    public void ComposeChangeSiteSummary_AppendsNote_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(
            MakeSiteItem(),
            new ChangeSiteRequest
            {
                ItemId = "site-item-1",
                NewSiteId = "site-99",
                NewSiteName = "Pitch 7",
                Note = "customer requested closer pitch"
            });

        Assert.Equal("Site changed: Pitch 4 → Pitch 7. Note: customer requested closer pitch", summary);
    }

    [Fact]
    public void ComposeMoveActivitySummary_DescribesOnlyChangedFields()
    {
        var summary = BookingActionCommentComposer.ComposeMoveActivitySummary(
            MakeActivityItem(),
            new MoveActivityRequest { ItemId = "act-item-1", NewStartTime = "14:00", NewEndTime = "16:00" });

        Assert.Equal(
            "Moved 'Archery Session': start time 10:00 → 14:00, end time 12:00 → 16:00.",
            summary);
    }

    [Fact]
    public void ComposeMoveActivitySummary_DescribesDateChange()
    {
        var summary = BookingActionCommentComposer.ComposeMoveActivitySummary(
            MakeActivityItem(),
            new MoveActivityRequest
            {
                ItemId = "act-item-1",
                NewStartDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)
            });

        Assert.Equal("Moved 'Archery Session': date 2 Aug 2026 → 3 Aug 2026.", summary);
    }

    [Fact]
    public void ComposeMoveDatesSummary_DescribesShift()
    {
        var summary = BookingActionCommentComposer.ComposeMoveDatesSummary(
            new MoveDatesRequest { DayShift = 7 });

        Assert.Equal("Dates shifted by 7 day(s).", summary);
    }

    [Fact]
    public void ComposeMoveDatesSummary_AppendsNote_WhenProvided()
    {
        var summary = BookingActionCommentComposer.ComposeMoveDatesSummary(
            new MoveDatesRequest { DayShift = -2, Note = "weather forecast" });

        Assert.Equal("Dates shifted by -2 day(s). Note: weather forecast", summary);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~BookingActionCommentComposerTests"`
Expected: FAIL — build error, `BookingActionCommentComposer` does not exist.

- [ ] **Step 4: Implement the composer**

Create `BookingsAssistant.Api/Services/BookingActionCommentComposer.cs`:
```csharp
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Builds human-readable audit-trail summaries for booking move actions. The result is
/// posted as an OSM comment by BookingActionsController after a successful mutation.
/// </summary>
public static class BookingActionCommentComposer
{
    public static string ComposeChangeSiteSummary(BookingItemDto original, ChangeSiteRequest request)
    {
        var newSiteLabel = string.IsNullOrWhiteSpace(request.NewSiteName)
            ? request.NewSiteId
            : request.NewSiteName;

        return AppendNote($"Site changed: {original.Label} → {newSiteLabel}.", request.Note);
    }

    public static string ComposeMoveActivitySummary(BookingItemDto original, MoveActivityRequest request)
    {
        var changes = new List<string>();

        if (request.NewStartDate.HasValue)
            changes.Add($"date {FormatDate(original.StartDate)} → {FormatDate(request.NewStartDate)}");

        if (request.NewStartTime != null)
            changes.Add($"start time {original.StartTime ?? "—"} → {request.NewStartTime}");

        if (request.NewEndTime != null)
            changes.Add($"end time {original.EndTime ?? "—"} → {request.NewEndTime}");

        var changeText = changes.Count > 0 ? string.Join(", ", changes) : "no fields changed";
        return AppendNote($"Moved '{original.Label}': {changeText}.", request.Note);
    }

    public static string ComposeMoveDatesSummary(MoveDatesRequest request)
        => AppendNote($"Dates shifted by {request.DayShift} day(s).", request.Note);

    private static string FormatDate(DateTime? date)
        => date.HasValue ? date.Value.ToString("d MMM yyyy") : "—";

    private static string AppendNote(string summary, string? note)
        => string.IsNullOrWhiteSpace(note) ? summary : $"{summary} Note: {note.Trim()}";
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~BookingActionCommentComposerTests"`
Expected: PASS — 7 tests passed.

- [ ] **Step 6: Commit**

```bash
git add BookingsAssistant.Api/Models/MoveActivityRequest.cs BookingsAssistant.Api/Models/ChangeSiteRequest.cs BookingsAssistant.Api/Models/MoveDatesRequest.cs BookingsAssistant.Api/Services/BookingActionCommentComposer.cs BookingsAssistant.Tests/Services/BookingActionCommentComposerTests.cs
git commit -m "feat: add BookingActionCommentComposer for move-action audit summaries"
```

---

### Task 2: Wire audit-comment posting into `BookingActionsController`

**Files:**
- Modify: `BookingsAssistant.Api/Controllers/BookingActionsController.cs`
- Test: `BookingsAssistant.Tests/Controllers/BookingActionsTests.cs`

**Interfaces:**
- Consumes: `BookingActionCommentComposer.ComposeChangeSiteSummary/ComposeMoveActivitySummary/ComposeMoveDatesSummary` (Task 1), `IOsmService.PostCommentAsync(string osmBookingId, string comment) -> Task<CommentDto?>` (existing), `BookingActionStatus.Completed`/`CompletedWithWarnings` (existing).
- Produces: no new public interface — behavior change only. `BookingActionResult.Status`/`Message` may now reflect a downgraded/annotated outcome.

- [ ] **Step 1: Write the failing integration tests**

Add to `BookingsAssistant.Tests/Controllers/BookingActionsTests.cs`, inside the class, after the existing `MoveDates_ReturnsRolledBack_WhenCreateFails` test (before the closing `}`):
```csharp
    // ── Audit-trail comments ─────────────────────────────────────────────────

    [Fact]
    public async Task MoveActivity_PostsAuditComment_WithComposedSummaryAndNote()
    {
        var bookingId = await SeedBookingAsync("99050");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "act-item-new" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "99050",
            OsmCommentId = "cmt-1",
            AuthorName = "Site Manager",
            TextPreview = "Moved 'Archery Session': start time 10:00 → 14:00, end time 12:00 → 16:00. Note: customer requested a later slot",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest
            {
                ItemId = "act-item-1",
                NewStartTime = "14:00",
                NewEndTime = "16:00",
                Note = "customer requested a later slot"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (osmBookingId, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal("99050", osmBookingId);
        Assert.Equal(
            "Moved 'Archery Session': start time 10:00 → 14:00, end time 12:00 → 16:00. Note: customer requested a later slot",
            comment);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.OsmComments.FirstOrDefaultAsync(c => c.OsmCommentId == "cmt-1");
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task MoveActivity_DowngradesToCompletedWithWarnings_WhenAuditCommentFailsToPost()
    {
        var bookingId = await SeedBookingAsync("99051");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "act-item-new" };
        _fakeOsm.CommentToReturn = null; // OSM comment post fails

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest { ItemId = "act-item-1", NewStartTime = "14:00" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.CompletedWithWarnings, result.Status);
        Assert.Contains("audit comment failed to post", result.Message);
    }

    [Fact]
    public async Task ChangeSite_PostsAuditComment_UsingNewSiteName()
    {
        var bookingId = await SeedBookingAsync("99052");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem("site-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "site-item-new" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "99052",
            OsmCommentId = "cmt-2",
            AuthorName = "Site Manager",
            TextPreview = "Site changed: Pitch A → Pitch 7.",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-site",
            new ChangeSiteRequest { ItemId = "site-item-1", NewSiteId = "site-99", NewSiteName = "Pitch 7" });

        var (_, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal("Site changed: Pitch A → Pitch 7.", comment);
    }

    [Fact]
    public async Task MoveDates_PostsAuditComment_WithDayShiftSummary()
    {
        var bookingId = await SeedBookingAsync("99053");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem("site-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "new-1" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "99053",
            OsmCommentId = "cmt-3",
            AuthorName = "Site Manager",
            TextPreview = "Dates shifted by 7 day(s).",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-dates",
            new MoveDatesRequest { DayShift = 7 });

        var (_, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal("Dates shifted by 7 day(s).", comment);
    }

    [Fact]
    public async Task MoveActivity_RolledBack_DoesNotPostAuditComment()
    {
        var bookingId = await SeedBookingAsync("99054");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("OSM create failed"));

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest { ItemId = "act-item-1", NewStartTime = "14:00" });

        Assert.Empty(_fakeOsm.CommentsPosted);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~BookingActionsTests"`
Expected: FAIL — the new assertions fail (e.g. `Assert.Single(_fakeOsm.CommentsPosted)` throws because the list is empty; the downgrade test sees `Status == "completed"` instead of `"completed_with_warnings"`).

- [ ] **Step 3: Implement the controller changes**

In `BookingsAssistant.Api/Controllers/BookingActionsController.cs`, add a private helper (place it after the `MoveDates` method, before the closing `}` of the class):
```csharp
    // ── Audit-trail comment posting ───────────────────────────────────────────
    // Runs after a mutation completes. Posts the given summary as an OSM comment and
    // persists it locally (same shape as BookingsController.PostComment) so it shows up
    // immediately. Only Completed/CompletedWithWarnings results get a comment — a rolled
    // back or failed move has nothing to summarize. A failed comment post never fails the
    // request; it downgrades the result to CompletedWithWarnings instead.
    private async Task PostAuditCommentAsync(Data.Entities.OsmBooking booking, BookingActionResult result, string summary)
    {
        if (result.Status != BookingActionStatus.Completed && result.Status != BookingActionStatus.CompletedWithWarnings)
            return;

        CommentDto? posted;
        try
        {
            posted = await _osmService.PostCommentAsync(booking.OsmBookingId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PostAuditCommentAsync: failed to post audit comment for booking {BookingId}",
                booking.OsmBookingId);
            posted = null;
        }

        if (posted == null)
        {
            result.Status = BookingActionStatus.CompletedWithWarnings;
            result.Message += "; audit comment failed to post";
            return;
        }

        _context.OsmComments.Add(new Data.Entities.OsmComment
        {
            OsmBookingId = booking.OsmBookingId,
            OsmCommentId = posted.OsmCommentId,
            AuthorName = posted.AuthorName,
            TextPreview = posted.TextPreview,
            CreatedDate = posted.CreatedDate,
            IsNew = false,
            LastFetched = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }
```

Then update each of the three actions to compose a summary and call the helper before returning. In `MoveActivity`, replace:
```csharp
            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, new[] { replacement });
            return Ok(result);
```
with:
```csharp
            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, new[] { replacement });
            var summary = BookingActionCommentComposer.ComposeMoveActivitySummary(item, request);
            await PostAuditCommentAsync(booking, result, summary);
            return Ok(result);
```

In `ChangeSite`, replace:
```csharp
            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, new[] { replacement });
            return Ok(result);
```
with:
```csharp
            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, new[] { replacement });
            var summary = BookingActionCommentComposer.ComposeChangeSiteSummary(item, request);
            await PostAuditCommentAsync(booking, result, summary);
            return Ok(result);
```

In `MoveDates`, replace:
```csharp
            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, replacements);
            return Ok(result);
```
with:
```csharp
            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, replacements);
            var summary = BookingActionCommentComposer.ComposeMoveDatesSummary(request);
            await PostAuditCommentAsync(booking, result, summary);
            return Ok(result);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~BookingActionsTests"`
Expected: PASS — all tests in the file pass, including the 5 new ones.

- [ ] **Step 5: Run the full backend test suite**

Run: `dotnet test`
Expected: PASS — no regressions in other test files (e.g. `BookingMutationServiceTests`, `CommentPostTests`).

- [ ] **Step 6: Commit**

```bash
git add BookingsAssistant.Api/Controllers/BookingActionsController.cs BookingsAssistant.Tests/Controllers/BookingActionsTests.cs
git commit -m "feat: post audit-trail comment to OSM after successful move actions"
```

---

### Task 3: Frontend — optional note field on each move action

**Files:**
- Modify: `BookingsAssistant.Web/src/types/index.ts`
- Modify: `BookingsAssistant.Web/src/components/BookingDetail.tsx`
- Create: `BookingsAssistant.Web/src/components/BookingDetail.auditNote.test.tsx`

**Interfaces:**
- Consumes: existing `bookingsApi.moveActivity/changeSite/moveDates` (unchanged signatures — the request object shape grows, but `apiClient.ts` forwards it as-is).
- Produces: no new exports — UI/behavior change only.

- [ ] **Step 0: Install frontend dependencies (one-time; `node_modules` doesn't exist in this worktree)**

Run:
```bash
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "S:\Work\bookings-helper\.worktrees\more-actions\BookingsAssistant.Web":/app \
  -w /app node:22-alpine npm install
```
Expected: completes with a `node_modules` directory created under `BookingsAssistant.Web`.

- [ ] **Step 1: Add the new request fields to the TypeScript types**

In `BookingsAssistant.Web/src/types/index.ts`, replace:
```typescript
/** Request to move an activity item (change time or date). */
export interface MoveActivityRequest {
  itemId: string;
  newStartDate?: string;
  newStartTime?: string;
  newEndTime?: string;
}

/** Request to move a site item to a different site. */
export interface ChangeSiteRequest {
  itemId: string;
  newSiteId: string;
}

/** Request to shift all items in a booking by the given number of days. */
export interface MoveDatesRequest {
  dayShift: number;
}
```
with:
```typescript
/** Request to move an activity item (change time or date). */
export interface MoveActivityRequest {
  itemId: string;
  newStartDate?: string;
  newStartTime?: string;
  newEndTime?: string;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to move a site item to a different site. */
export interface ChangeSiteRequest {
  itemId: string;
  newSiteId: string;
  /** Display name of the target site, shown in the available-sites dropdown. */
  newSiteName?: string;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}

/** Request to shift all items in a booking by the given number of days. */
export interface MoveDatesRequest {
  dayShift: number;
  /** Optional free-text note appended to the auto-generated audit comment. */
  note?: string;
}
```

- [ ] **Step 2: Write the failing RTL tests**

Create `BookingsAssistant.Web/src/components/BookingDetail.auditNote.test.tsx`:
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import BookingDetail from './BookingDetail';
import { bookingsApi } from '../services/apiClient';
import type { BookingActionResult, BookingItem } from '../types';

vi.mock('../services/apiClient', () => ({
  bookingsApi: {
    getById: vi.fn(), getItems: vi.fn(), getAvailableSites: vi.fn(),
    moveDates: vi.fn(), moveActivity: vi.fn(), changeSite: vi.fn(),
  },
}));
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const api = bookingsApi as any;

const booking = {
  id: 1, osmBookingId: '179743', customerName: 'Test', startDate: '2027-12-04',
  endDate: '2027-12-05', status: 'Provisional', fullDetails: '', comments: [], linkedEmails: [],
};
const activity: BookingItem = { itemId: '411468', type: 'activity', activityId: '4961', label: 'Air Rifle', startDate: '2027-12-05', startTime: '09:00', endTime: '10:00' };
const site: BookingItem = { itemId: '411467', type: 'site', siteId: '1387', label: 'Hayvern', startDate: '2027-12-04', endDate: '2027-12-05' };

const ok = (items: BookingItem[]): BookingActionResult =>
  ({ created: ['999'], deleted: ['x'], status: 'completed', message: 'Replaced 1 item(s) successfully.', items });

function renderDetail() {
  return render(
    <MemoryRouter initialEntries={['/bookings/1']}>
      <Routes><Route path="/bookings/:id" element={<BookingDetail />} /></Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  api.getById.mockResolvedValue(booking);
  api.getItems.mockResolvedValue([activity, site]);
  api.getAvailableSites.mockResolvedValue([
    { id: '1387', name: 'Hayvern' }, { id: '1404', name: 'Birch' },
  ]);
});

describe('audit-trail note field', () => {
  it('sends the note with a move-activity confirm', async () => {
    api.moveActivity.mockResolvedValue(ok([activity]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'customer requested a later slot');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveActivity).toHaveBeenCalledWith(1, {
      itemId: '411468', newStartTime: '14:00', note: 'customer requested a later slot',
    }));
  });

  it('omits note from the move-activity request when left blank', async () => {
    api.moveActivity.mockResolvedValue(ok([activity]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveActivity).toHaveBeenCalledWith(1, {
      itemId: '411468', newStartTime: '14:00',
    }));
  });

  it('sends the note and resolved site name with a change-site confirm', async () => {
    api.changeSite.mockResolvedValue(ok([site]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /change site/i }));
    await user.selectOptions(await screen.findByLabelText(/new site/i), '1404');
    await user.click(screen.getByRole('button', { name: /^change$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'customer requested closer pitch');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.changeSite).toHaveBeenCalledWith(1, {
      itemId: '411467', newSiteId: '1404', newSiteName: 'Birch', note: 'customer requested closer pitch',
    }));
  });

  it('sends the note with a move-dates confirm', async () => {
    api.moveDates.mockResolvedValue(ok([site]));
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.type(screen.getByLabelText(/shift all booking dates/i), '7');
    await user.click(screen.getByRole('button', { name: /^move dates$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'weather forecast');
    await user.click(screen.getByRole('button', { name: /confirm/i }));

    await waitFor(() => expect(api.moveDates).toHaveBeenCalledWith(1, {
      dayShift: 7, note: 'weather forecast',
    }));
  });

  it('clears the note when the form is closed and reopened', async () => {
    const user = userEvent.setup();
    renderDetail();
    await screen.findByText(/Booking #179743/);

    await user.click(screen.getByRole('button', { name: /move activity/i }));
    await user.type(screen.getByLabelText(/new start time/i), '14:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));
    await user.type(screen.getByLabelText(/add a note/i), 'draft note');
    await user.click(screen.getByRole('button', { name: /cancel/i })); // back to pre-confirm stage
    await user.click(screen.getByRole('button', { name: /cancel/i })); // closes the form entirely

    await user.click(screen.getByRole('button', { name: /move activity/i })); // reopen
    await user.type(screen.getByLabelText(/new start time/i), '15:00');
    await user.click(screen.getByRole('button', { name: /^move$/i }));

    expect(screen.getByLabelText(/add a note/i)).toHaveValue('');
  });
});
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:
```bash
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "S:\Work\bookings-helper\.worktrees\more-actions\BookingsAssistant.Web":/app \
  -w /app node:22-alpine npm test -- BookingDetail.auditNote
```
Expected: FAIL — `screen.getByLabelText(/add a note/i)` finds no element (the textarea doesn't exist yet).

- [ ] **Step 4: Implement the component changes**

In `BookingsAssistant.Web/src/components/BookingDetail.tsx`:

4a. Update the type import (top of file) to include `MoveDatesRequest`:
```typescript
import type {
  AvailableSite,
  BookingActionResult,
  BookingDetail as BookingDetailType,
  BookingItem,
  ChangeSiteRequest,
  MoveActivityRequest,
  MoveDatesRequest,
} from '../types';
```

4b. Add `note` state alongside the other per-item action state:
```typescript
  const [newSiteId, setNewSiteId] = useState('');
  const [note, setNote] = useState('');
```

4c. Reset `note` wherever the other per-item fields are reset. In `openItemAction`:
```typescript
  const openItemAction = (itemId: string, kind: ItemActionKind) => {
    setActiveItemAction({ itemId, kind });
    setConfirmingItemAction(false);
    setNewStartDate('');
    setNewStartTime('');
    setNewEndTime('');
    setNewSiteId('');
    setNote('');
  };
```

And in `runAction`'s `finally` block:
```typescript
    } finally {
      setActionInProgress(false);
      setConfirmingMoveDates(false);
      setActiveItemAction(null);
      setConfirmingItemAction(false);
      setNote('');
    }
```

4d. Thread `note` (and the resolved site name) into the three handlers:
```typescript
  const handleMoveDates = () => {
    const shift = parseInt(dayShift, 10);
    if (!id || Number.isNaN(shift) || shift === 0) return;
    const req: MoveDatesRequest = { dayShift: shift };
    if (note.trim()) req.note = note.trim();
    runAction(() => bookingsApi.moveDates(parseInt(id), req));
  };
```
```typescript
  const handleMoveActivity = (itemId: string) => {
    if (!id) return;
    const req: MoveActivityRequest = { itemId };
    if (newStartDate) req.newStartDate = newStartDate;
    if (newStartTime) req.newStartTime = newStartTime;
    if (newEndTime) req.newEndTime = newEndTime;
    if (note.trim()) req.note = note.trim();
    runAction(() => bookingsApi.moveActivity(parseInt(id), req));
  };
```
```typescript
  const handleChangeSite = (itemId: string) => {
    if (!id || !newSiteId) return;
    const req: ChangeSiteRequest = { itemId, newSiteId };
    const selectedSite = availableSites.find((s) => s.id === newSiteId);
    if (selectedSite) req.newSiteName = selectedSite.name;
    if (note.trim()) req.note = note.trim();
    runAction(() => bookingsApi.changeSite(parseInt(id), req));
  };
```

4e. Add the note textarea to the move-dates confirm block. Replace:
```tsx
            ) : (
              <span className="text-sm text-gray-700">
                Recreate every item shifted by {dayShift} day(s), then delete the originals?
                <button
                  onClick={handleMoveDates}
                  disabled={actionInProgress}
                  className="ml-2 px-3 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50"
                >
                  {actionInProgress ? 'Working…' : 'Confirm'}
                </button>
                <button
                  onClick={() => setConfirmingMoveDates(false)}
                  disabled={actionInProgress}
                  className="ml-2 px-3 py-1 bg-gray-300 text-gray-800 rounded hover:bg-gray-400 disabled:opacity-50"
                >
                  Cancel
                </button>
              </span>
            )}
```
with:
```tsx
            ) : (
              <div className="text-sm text-gray-700 space-y-2">
                <p>Recreate every item shifted by {dayShift} day(s), then delete the originals?</p>
                <div>
                  <label htmlFor="move-dates-note" className="block text-sm font-medium text-gray-700">Add a note (optional)</label>
                  <textarea id="move-dates-note" value={note}
                    onChange={(e) => setNote(e.target.value)} disabled={actionInProgress}
                    className="w-full p-2 border border-gray-300 rounded resize-none" rows={2} />
                </div>
                <div>
                  <button
                    onClick={handleMoveDates}
                    disabled={actionInProgress}
                    className="px-3 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50"
                  >
                    {actionInProgress ? 'Working…' : 'Confirm'}
                  </button>
                  <button
                    onClick={() => setConfirmingMoveDates(false)}
                    disabled={actionInProgress}
                    className="ml-2 px-3 py-1 bg-gray-300 text-gray-800 rounded hover:bg-gray-400 disabled:opacity-50"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            )}
```

4f. Add the note textarea to the move-activity confirm block. Replace:
```tsx
                    ) : (
                      <div className="text-sm text-gray-700">
                        Recreate this activity at the new time, then delete the original?
                        <button onClick={() => handleMoveActivity(item.itemId)} disabled={actionInProgress}
                          className="ml-2 px-3 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50">
                          {actionInProgress ? 'Working…' : 'Confirm'}</button>
                        <button onClick={() => setConfirmingItemAction(false)} disabled={actionInProgress}
                          className="ml-2 px-3 py-1 bg-gray-300 text-gray-800 rounded hover:bg-gray-400">Cancel</button>
                      </div>
                    )}
```
with:
```tsx
                    ) : (
                      <div className="text-sm text-gray-700 space-y-2">
                        <p>Recreate this activity at the new time, then delete the original?</p>
                        <div>
                          <label htmlFor={`note-${item.itemId}`} className="block text-sm font-medium text-gray-700">Add a note (optional)</label>
                          <textarea id={`note-${item.itemId}`} value={note}
                            onChange={(e) => setNote(e.target.value)} disabled={actionInProgress}
                            className="w-full p-2 border border-gray-300 rounded resize-none" rows={2} />
                        </div>
                        <div>
                          <button onClick={() => handleMoveActivity(item.itemId)} disabled={actionInProgress}
                            className="px-3 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50">
                            {actionInProgress ? 'Working…' : 'Confirm'}</button>
                          <button onClick={() => setConfirmingItemAction(false)} disabled={actionInProgress}
                            className="ml-2 px-3 py-1 bg-gray-300 text-gray-800 rounded hover:bg-gray-400">Cancel</button>
                        </div>
                      </div>
                    )}
```

4g. Add the note textarea to the change-site confirm block. Replace:
```tsx
                          ) : (
                            <div className="text-sm text-gray-700">
                              Move this booking to the new site? The booking keeps its ID and payments — only the pitch changes.
                              <button onClick={() => handleChangeSite(item.itemId)} disabled={actionInProgress}
                                className="ml-2 px-3 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50">
                                {actionInProgress ? 'Working…' : 'Confirm'}</button>
                              <button onClick={() => setConfirmingItemAction(false)} disabled={actionInProgress}
                                className="ml-2 px-3 py-1 bg-gray-300 text-gray-800 rounded hover:bg-gray-400">Cancel</button>
                            </div>
                          )}
```
with:
```tsx
                          ) : (
                            <div className="text-sm text-gray-700 space-y-2">
                              <p>Move this booking to the new site? The booking keeps its ID and payments — only the pitch changes.</p>
                              <div>
                                <label htmlFor={`note-${item.itemId}`} className="block text-sm font-medium text-gray-700">Add a note (optional)</label>
                                <textarea id={`note-${item.itemId}`} value={note}
                                  onChange={(e) => setNote(e.target.value)} disabled={actionInProgress}
                                  className="w-full p-2 border border-gray-300 rounded resize-none" rows={2} />
                              </div>
                              <div>
                                <button onClick={() => handleChangeSite(item.itemId)} disabled={actionInProgress}
                                  className="px-3 py-1 bg-red-600 text-white rounded hover:bg-red-700 disabled:opacity-50">
                                  {actionInProgress ? 'Working…' : 'Confirm'}</button>
                                <button onClick={() => setConfirmingItemAction(false)} disabled={actionInProgress}
                                  className="ml-2 px-3 py-1 bg-gray-300 text-gray-800 rounded hover:bg-gray-400">Cancel</button>
                              </div>
                            </div>
                          )}
```

- [ ] **Step 5: Run the new tests to verify they pass**

Run:
```bash
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "S:\Work\bookings-helper\.worktrees\more-actions\BookingsAssistant.Web":/app \
  -w /app node:22-alpine npm test -- BookingDetail.auditNote
```
Expected: PASS — all 5 tests pass.

- [ ] **Step 6: Run the full frontend test suite to check for regressions**

Run:
```bash
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "S:\Work\bookings-helper\.worktrees\more-actions\BookingsAssistant.Web":/app \
  -w /app node:22-alpine npm test
```
Expected: PASS — `BookingDetail.itemActions.test.tsx` and `BookingDetail.moveDates.test.tsx` still pass (button roles/names are unchanged; only the confirm-stage markup gained a wrapping `<div>`/`<p>` and a new textarea).

- [ ] **Step 7: Lint**

Run:
```bash
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "S:\Work\bookings-helper\.worktrees\more-actions\BookingsAssistant.Web":/app \
  -w /app node:22-alpine npm run lint
```
Expected: PASS — no lint errors.

- [ ] **Step 8: Commit**

```bash
git add BookingsAssistant.Web/src/types/index.ts BookingsAssistant.Web/src/components/BookingDetail.tsx BookingsAssistant.Web/src/components/BookingDetail.auditNote.test.tsx
git commit -m "feat: add optional audit-trail note field to move-action confirm steps"
```
