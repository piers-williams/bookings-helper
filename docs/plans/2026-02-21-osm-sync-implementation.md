# OSM Booking Sync Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Persist bookings fetched from OSM into the `OsmBookings` table so that email→booking matching works with real data.

**Architecture:** Two additions to `BookingsController`: a private `UpsertBookingsAsync` helper that inserts/updates `OsmBooking` rows, a write-through call in `GET /api/bookings` that persists live data as a side effect, and a new `POST /api/bookings/sync` endpoint that fetches provisional + confirmed bookings in parallel and returns a count summary. Tests use a `FakeOsmService` stub registered via `WithWebHostBuilder`.

**Tech Stack:** .NET 8 ASP.NET Core, EF Core 8 (SQLite / InMemory for tests), xUnit, `WebApplicationFactory<Program>`

**Working directory:** `.worktrees/bookings-assistant-mvp/`

---

## Task 1: Add failing tests

**Files:**
- Create: `BookingsAssistant.Tests/Controllers/OsmSyncTests.cs`
- Test: `BookingsAssistant.Tests/Controllers/OsmSyncTests.cs`

**Context:** The test project already exists with the correct project references and `WebApplicationFactory` setup. See `BookingsAssistant.Tests/Controllers/EmailCaptureTests.cs` for the pattern to follow — critically, `Guid.NewGuid()` for the in-memory DB name must be captured **outside** the `AddDbContext` lambda or each scope gets a different database.

`IOsmService` is registered in `Program.cs` via `AddHttpClient<IOsmService, OsmService>()`. We replace it with a simple stub using `services.RemoveAll<IOsmService>()` + `services.AddSingleton<IOsmService>(fakeInstance)`.

**Step 1: Create the test file**

Create `BookingsAssistant.Tests/Controllers/OsmSyncTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class OsmSyncTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm = new();

    public OsmSyncTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_OsmSync_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQLite with in-memory database for tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // Replace real OsmService (which needs live OAuth) with a controllable fake
                services.RemoveAll<IOsmService>();
                services.AddSingleton<IOsmService>(_fakeOsm);
            });
        });
    }

    [Fact]
    public async Task GetBookings_UpsertsFetchedBookingsToDatabase()
    {
        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "55001", CustomerName = "Scouts UK",
                    StartDate = DateTime.UtcNow.AddDays(10), EndDate = DateTime.UtcNow.AddDays(12),
                    Status = "Provisional" }
        };

        var client = _factory.CreateClient();
        await client.GetAsync("/api/bookings");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = await db.OsmBookings.FirstOrDefaultAsync(b => b.OsmBookingId == "55001");
        Assert.NotNull(booking);
        Assert.Equal("Scouts UK", booking.CustomerName);
    }

    [Fact]
    public async Task SyncEndpoint_InsertsNewAndUpdatesExistingBookings()
    {
        // Seed an existing booking that will be updated by sync
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new BookingsAssistant.Api.Data.Entities.OsmBooking
            {
                OsmBookingId = "55002",
                CustomerName = "Old Name",
                StartDate = DateTime.UtcNow.AddDays(5),
                EndDate = DateTime.UtcNow.AddDays(7),
                Status = "Provisional",
                LastFetched = DateTime.UtcNow.AddHours(-2)
            });
            await db.SaveChangesAsync();
        }

        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "55002", CustomerName = "Updated Name",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Confirmed" },
            new() { OsmBookingId = "55003", CustomerName = "New Group",
                    StartDate = DateTime.UtcNow.AddDays(20), EndDate = DateTime.UtcNow.AddDays(22),
                    Status = "Provisional" }
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/bookings/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Added);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = await db2.OsmBookings.FirstAsync(b => b.OsmBookingId == "55002");
        Assert.Equal("Updated Name", updated.CustomerName);
        Assert.Equal("Confirmed", updated.Status);
        var added = await db2.OsmBookings.FirstOrDefaultAsync(b => b.OsmBookingId == "55003");
        Assert.NotNull(added);
    }

    // Stub — returns whatever BookingsToReturn is set to at call time
    private class FakeOsmService : IOsmService
    {
        public List<BookingDto> BookingsToReturn { get; set; } = new();

        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(BookingsToReturn);

        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
```

**Step 2: Run tests to confirm they fail (endpoint and model don't exist yet)**

```bash
cd .worktrees/bookings-assistant-mvp
dotnet test BookingsAssistant.Tests --filter "OsmSync" -v minimal 2>&1 | tail -15
```

Expected: build error referencing missing `SyncResult`, or tests fail with 404/405.

**Step 3: Commit**

```bash
git add BookingsAssistant.Tests/Controllers/OsmSyncTests.cs
git commit -m "test: add failing tests for OSM booking sync"
```

---

## Task 2: Implement the sync

**Files:**
- Create: `BookingsAssistant.Api/Models/SyncResult.cs`
- Modify: `BookingsAssistant.Api/Controllers/BookingsController.cs`

### Step 1: Create `SyncResult.cs`

Create `BookingsAssistant.Api/Models/SyncResult.cs`:

```csharp
namespace BookingsAssistant.Api.Models;

public class SyncResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Total => Added + Updated;
}
```

### Step 2: Update `BookingsController.cs`

The full updated file. Changes are:
1. `GetProvisional` — call `UpsertBookingsAsync` after a successful OSM fetch (errors swallowed so a DB hiccup doesn't break the live list)
2. New `Sync` action — fetches provisional + confirmed in parallel, deduplicates, upserts, returns `SyncResult`
3. New private `UpsertBookingsAsync` — batch upsert by `OsmBookingId`

Open `BookingsAssistant.Api/Controllers/BookingsController.cs` and replace the entire file contents with:

```csharp
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly ILinkingService _linkingService;
    private readonly ApplicationDbContext _context;
    private readonly IOsmService _osmService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(ILinkingService linkingService, ApplicationDbContext context,
        IOsmService osmService, ILogger<BookingsController> logger)
    {
        _linkingService = linkingService;
        _context = context;
        _osmService = osmService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> GetProvisional([FromQuery] string? status = "Provisional")
    {
        try
        {
            var bookings = await _osmService.GetBookingsAsync(status ?? "Provisional");

            // Write-through: persist to DB as a side effect so email linking works
            if (bookings.Any())
            {
                try { await UpsertBookingsAsync(bookings); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to cache bookings from OSM"); }
            }

            return Ok(bookings);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching bookings from OSM", detail = ex.Message });
        }
    }

    [HttpPost("sync")]
    [Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
    public async Task<ActionResult<SyncResult>> Sync()
    {
        try
        {
            // Fetch provisional and confirmed in parallel
            var provisionalTask = _osmService.GetBookingsAsync("provisional");
            var confirmedTask = _osmService.GetBookingsAsync("confirmed");
            await Task.WhenAll(provisionalTask, confirmedTask);

            // Merge, deduplicating by OsmBookingId (provisional wins if duplicated)
            var allBookings = provisionalTask.Result
                .Concat(confirmedTask.Result)
                .GroupBy(b => b.OsmBookingId)
                .Select(g => g.First())
                .ToList();

            var result = await UpsertBookingsAsync(allBookings);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Error syncing bookings from OSM", detail = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDetailDto>> GetById(int id)
    {
        // Mock data for now
        var booking = new BookingDetailDto
        {
            Id = id,
            OsmBookingId = "12345",
            CustomerName = "John Smith",
            CustomerEmail = "john@scouts.org.uk",
            StartDate = new DateTime(2026, 3, 15),
            EndDate = new DateTime(2026, 3, 17),
            Status = "Provisional",
            FullDetails = "{\"site\": \"Main Field\", \"attendees\": 25}",
            Comments = new List<CommentDto>
            {
                new CommentDto
                {
                    Id = 1,
                    OsmBookingId = "12345",
                    OsmCommentId = "c1",
                    AuthorName = "Tammy",
                    TextPreview = "Called customer to confirm arrival time",
                    CreatedDate = DateTime.UtcNow.AddDays(-1),
                    IsNew = false
                }
            },
            LinkedEmails = new List<EmailDto>()
        };

        var linkedEmailIds = await _linkingService.GetLinkedEmailIdsAsync(id);
        if (linkedEmailIds.Any())
        {
            var linkedEmails = await _context.EmailMessages
                .Where(e => linkedEmailIds.Contains(e.Id))
                .OrderByDescending(e => e.ReceivedDate)
                .Select(e => new EmailDto
                {
                    Id = e.Id,
                    SenderEmail = e.SenderEmail,
                    SenderName = e.SenderName,
                    Subject = e.Subject,
                    ReceivedDate = e.ReceivedDate,
                    IsRead = e.IsRead,
                    ExtractedBookingRef = e.ExtractedBookingRef
                })
                .ToListAsync();

            booking.LinkedEmails = linkedEmails;
        }

        return Ok(booking);
    }

    [HttpGet("{id}/links")]
    [Microsoft.AspNetCore.Cors.EnableCors("ExtensionCapture")]
    public async Task<ActionResult<List<EmailDto>>> GetLinks(int id)
    {
        var linkedEmailIds = await _linkingService.GetLinkedEmailIdsAsync(id);
        var emails = await _context.EmailMessages
            .Where(e => linkedEmailIds.Contains(e.Id))
            .OrderByDescending(e => e.ReceivedDate)
            .Select(e => new EmailDto
            {
                Id = e.Id,
                SenderEmail = e.SenderEmail,
                SenderName = e.SenderName,
                Subject = e.Subject,
                ReceivedDate = e.ReceivedDate,
                IsRead = e.IsRead,
                ExtractedBookingRef = e.ExtractedBookingRef
            })
            .ToListAsync();

        return Ok(emails);
    }

    private async Task<SyncResult> UpsertBookingsAsync(List<BookingDto> bookings)
    {
        var osmIds = bookings.Select(b => b.OsmBookingId).ToList();
        var existing = await _context.OsmBookings
            .Where(b => osmIds.Contains(b.OsmBookingId))
            .ToDictionaryAsync(b => b.OsmBookingId);

        int added = 0, updated = 0;

        foreach (var booking in bookings)
        {
            if (existing.TryGetValue(booking.OsmBookingId, out var entity))
            {
                entity.CustomerName = booking.CustomerName;
                entity.StartDate = booking.StartDate;
                entity.EndDate = booking.EndDate;
                entity.Status = booking.Status;
                entity.LastFetched = DateTime.UtcNow;
                updated++;
            }
            else
            {
                _context.OsmBookings.Add(new Data.Entities.OsmBooking
                {
                    OsmBookingId = booking.OsmBookingId,
                    CustomerName = booking.CustomerName,
                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    Status = booking.Status,
                    LastFetched = DateTime.UtcNow
                });
                added++;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("OSM sync: {Added} added, {Updated} updated", added, updated);
        return new SyncResult { Added = added, Updated = updated };
    }
}
```

### Step 3: Run tests

```bash
cd .worktrees/bookings-assistant-mvp
dotnet test BookingsAssistant.Tests -v minimal 2>&1 | tail -5
```

Expected: `Passed! Failed: 0, Passed: 5` (3 existing + 2 new).

### Step 4: Commit

```bash
git add BookingsAssistant.Api/Models/SyncResult.cs BookingsAssistant.Api/Controllers/BookingsController.cs
git commit -m "feat: upsert OSM bookings to DB on GET /api/bookings and POST /api/bookings/sync"
```

---

## Task 3: Manual smoke test

**Step 1: Start the backend**

```bash
cd .worktrees/bookings-assistant-mvp/BookingsAssistant.Api
dotnet run --urls "http://localhost:5000"
```

**Step 2: Trigger a sync**

```bash
curl -s -X POST http://localhost:5000/api/bookings/sync | python -m json.tool
```

Expected response (numbers will vary):
```json
{ "added": 12, "updated": 0, "total": 12 }
```

**Step 3: Verify bookings are in the database**

```bash
curl -s http://localhost:5000/api/bookings | python -m json.tool | head -30
```

**Step 4: Open an email in OWA that references a real booking ref**

The sidebar should now show the linked booking card instead of "No booking linked".

**Step 5: Commit test file if anything was adjusted**

```bash
git add -A
git commit -m "feat: OSM sync smoke test passed"
```
