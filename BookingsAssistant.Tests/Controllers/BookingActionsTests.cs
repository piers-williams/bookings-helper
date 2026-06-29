using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class BookingActionsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_BookingActions_" + Guid.NewGuid();
        _fakeOsm = new FakeOsmService();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hashing:Iterations"] = "1"
                }));

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.RemoveAll<IOsmService>();
                services.AddSingleton<IOsmService>(_fakeOsm);
            });
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<int> SeedBookingAsync(string osmBookingId = "99001")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var booking = new OsmBooking
        {
            OsmBookingId = osmBookingId,
            CustomerName = "Test Group",
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            Status = "Provisional"
        };
        db.OsmBookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private static BookingItemDto MakeActivityItem(string itemId = "act-item-1") => new()
    {
        ItemId = itemId,
        Type = "activity",
        ActivityId = "act-10",
        Label = "Archery Session",
        StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
        StartTime = "10:00",
        EndTime = "12:00"
    };

    private static BookingItemDto MakeSiteItem(string itemId = "site-item-1") => new()
    {
        ItemId = itemId,
        Type = "site",
        SiteId = "site-42",
        Label = "Pitch A",
        StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)
    };

    // ── move-activity ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveActivity_Returns200WithCompletedResult_WhenHappyPath()
    {
        var bookingId = await SeedBookingAsync("99010");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "act-item-new" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest
            {
                ItemId = "act-item-1",
                NewStartTime = "14:00",
                NewEndTime = "16:00"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Contains("act-item-new", result.Created);
        Assert.Contains("act-item-1", result.Deleted);
    }

    [Fact]
    public async Task MoveActivity_OverridesReachEngine_ViaCloneJson()
    {
        var bookingId = await SeedBookingAsync("99011");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest
            {
                ItemId = "act-item-1",
                NewStartDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
                NewStartTime = "09:00",
                NewEndTime = "11:00"
            });

        Assert.Single(_fakeOsm.CapturedCloneJsons);
        var cloneJson = _fakeOsm.CapturedCloneJsons[0];
        Assert.Contains("09:00", cloneJson);
        Assert.Contains("11:00", cloneJson);
        // The shifted start date should appear in the serialised clone
        Assert.Contains("2026-08-03", cloneJson);
    }

    [Fact]
    public async Task MoveActivity_Returns400_WhenItemIdMissing()
    {
        var bookingId = await SeedBookingAsync("99012");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest { ItemId = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MoveActivity_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/bookings/999999/actions/move-activity",
            new MoveActivityRequest { ItemId = "some-item" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MoveActivity_Returns404_WhenItemNotInBooking()
    {
        var bookingId = await SeedBookingAsync("99013");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest { ItemId = "item-does-not-exist" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── change-site ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeSite_Returns200WithCompletedResult_WhenHappyPath()
    {
        var bookingId = await SeedBookingAsync("99020");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem("site-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "site-item-new" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-site",
            new ChangeSiteRequest
            {
                ItemId = "site-item-1",
                NewSiteId = "site-99"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Contains("site-item-new", result.Created);
        Assert.Contains("site-item-1", result.Deleted);

        // Clone JSON should contain the new site id
        Assert.Single(_fakeOsm.CapturedCloneJsons);
        Assert.Contains("site-99", _fakeOsm.CapturedCloneJsons[0]);
    }

    [Fact]
    public async Task ChangeSite_Returns400_WhenNewSiteIdMissing()
    {
        var bookingId = await SeedBookingAsync("99021");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-site",
            new ChangeSiteRequest { ItemId = "site-item-1", NewSiteId = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSite_Returns400_WhenItemIdMissing()
    {
        var bookingId = await SeedBookingAsync("99022");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-site",
            new ChangeSiteRequest { ItemId = "", NewSiteId = "site-99" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSite_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/bookings/999999/actions/change-site",
            new ChangeSiteRequest { ItemId = "x", NewSiteId = "y" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeSite_Returns404_WhenItemNotInBooking()
    {
        var bookingId = await SeedBookingAsync("99023");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeSiteItem("site-item-1") };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-site",
            new ChangeSiteRequest { ItemId = "item-does-not-exist", NewSiteId = "site-99" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── move-dates ────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveDates_Returns400_WhenDayShiftIsZero()
    {
        var bookingId = await SeedBookingAsync("99030");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-dates",
            new MoveDatesRequest { DayShift = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MoveDates_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/bookings/999999/actions/move-dates",
            new MoveDatesRequest { DayShift = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MoveDates_Returns200AndFansOutOneReplacementPerItem()
    {
        var bookingId = await SeedBookingAsync("99031");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>
        {
            MakeSiteItem("site-item-1"),
            MakeActivityItem("act-item-1"),
            MakeSiteItem("site-item-2")
        };
        _fakeOsm.CreatedItemIds = new List<string> { "new-1", "new-2", "new-3" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-dates",
            new MoveDatesRequest { DayShift = 7 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Completed, result.Status);

        // One create per item
        Assert.Equal(3, _fakeOsm.CapturedCloneJsons.Count);

        // Dates should be shifted by 7 days — site-item-1 starts 2026-08-01, shifted to 2026-08-08
        Assert.Contains("2026-08-08", _fakeOsm.CapturedCloneJsons[0]);
    }

    [Fact]
    public async Task MoveDates_ShiftsDates_AndPreservesTimes()
    {
        var bookingId = await SeedBookingAsync("99032");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>
        {
            new BookingItemDto
            {
                ItemId = "act-item-1",
                Type = "activity",
                Label = "Archery",
                StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                StartTime = "10:00",
                EndTime = "12:00"
            }
        };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-dates",
            new MoveDatesRequest { DayShift = 3 });

        Assert.Single(_fakeOsm.CapturedCloneJsons);
        var cloneJson = _fakeOsm.CapturedCloneJsons[0];
        // Date shifted: 2026-08-02 + 3 days = 2026-08-05
        Assert.Contains("2026-08-05", cloneJson);
        // Times preserved
        Assert.Contains("10:00", cloneJson);
        Assert.Contains("12:00", cloneJson);
    }

    [Fact]
    public async Task MoveDates_WithMixOfDatedAndDatelessItems_ShiftsDatedItemOnly_LeavesDatelessItemUntouched()
    {
        var bookingId = await SeedBookingAsync("99033");

        // One item WITH a StartDate, one item WITHOUT a StartDate
        var datedItem = new BookingItemDto
        {
            ItemId = "site-item-dated",
            Type = "site",
            SiteId = "site-10",
            Label = "Pitch A",
            StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)
        };
        var datelessItem = new BookingItemDto
        {
            ItemId = "act-item-dateless",
            Type = "activity",
            ActivityId = "act-99",
            Label = "Badge Award",
            StartDate = null,
            EndDate = null
        };
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { datedItem, datelessItem };
        _fakeOsm.CreatedItemIds = new List<string> { "new-dated", "new-dateless" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-dates",
            new MoveDatesRequest { DayShift = 7 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Fan-out: one replacement per item regardless of whether it has dates
        Assert.Equal(2, _fakeOsm.CapturedCloneJsons.Count);

        // Dated item clone: start date shifted from 2026-08-01 by 7 days → 2026-08-08
        var datedCloneJson = _fakeOsm.CapturedCloneJsons[0];
        Assert.Contains("2026-08-08", datedCloneJson);

        // Dateless item clone: no date override — null dates are not shifted,
        // so the clone JSON must NOT contain any date value
        var datelessCloneJson = _fakeOsm.CapturedCloneJsons[1];
        Assert.DoesNotContain("2026-08", datelessCloneJson);
    }

    // ── Status propagation ────────────────────────────────────────────────────

    [Fact]
    public async Task MoveActivity_ReturnsRolledBack_WhenCreateFails()
    {
        var bookingId = await SeedBookingAsync("99040");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("OSM create failed"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest { ItemId = "act-item-1", NewStartTime = "14:00" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.RolledBack, result.Status);
    }

    [Fact]
    public async Task MoveActivity_ReturnsCompletedWithWarnings_WhenDeleteFails()
    {
        var bookingId = await SeedBookingAsync("99041");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "act-item-new" };
        _fakeOsm.DeleteReturnFalseForIds.Add("act-item-1");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-activity",
            new MoveActivityRequest { ItemId = "act-item-1", NewStartTime = "14:00" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.CompletedWithWarnings, result.Status);
    }

    [Fact]
    public async Task MoveDates_ReturnsRolledBack_WhenCreateFails()
    {
        var bookingId = await SeedBookingAsync("99042");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto>
        {
            MakeSiteItem("site-item-1"),
            MakeSiteItem("site-item-2")
        };
        // Fail on the 2nd create call
        _fakeOsm.FailCreateOnCall = (2, new InvalidOperationException("OSM create failed on 2nd"));
        _fakeOsm.CreatedItemIds = new List<string> { "new-1" };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/move-dates",
            new MoveDatesRequest { DayShift = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.RolledBack, result.Status);
    }
}
