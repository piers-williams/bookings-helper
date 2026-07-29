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

/// <summary>
/// Covers POST .../actions/change-numbers — a clone-then-delete-original headcount change
/// (via IBookingItemActionService.ChangeNumbersAsync / IBookingMutationService.ReplaceItemsAsync),
/// the same engine MoveActivity/ChangeSite use — not a standalone delete or a from-scratch create.
/// </summary>
public class BookingActionsChangeNumbersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsChangeNumbersTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_ChangeNumbers_" + Guid.NewGuid();
        _fakeOsm = new FakeOsmService();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Hashing:Iterations"] = "1" }));
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.RemoveAll<IOsmService>();
                services.AddSingleton<IOsmService>(_fakeOsm);
            });
        });
    }

    private async Task<int> SeedBookingAsync(string osmBookingId)
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
        EndTime = "12:00",
        NumberPeople = 4
    };

    // ── change-numbers ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeNumbers_Returns200WithCompletedResult_WhenHappyPath()
    {
        var bookingId = await SeedBookingAsync("97010");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "act-item-new" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "97010",
            OsmCommentId = "cmt-change-numbers-1",
            AuthorName = "Site Manager",
            TextPreview = "audit comment",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = 10 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Contains("act-item-new", result.Created);
        Assert.Contains("act-item-1", result.Deleted);

        var spec = Assert.Single(_fakeOsm.CapturedSpecs);
        Assert.Equal(10, spec.NumberPeople);
        // Other fields unchanged from the original
        Assert.Equal("10:00", spec.StartTime);
        Assert.Equal("12:00", spec.EndTime);
    }

    [Fact]
    public async Task ChangeNumbers_PostsAuditComment_OnSuccess()
    {
        var bookingId = await SeedBookingAsync("97011");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CreatedItemIds = new List<string> { "act-item-new" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "97011",
            OsmCommentId = "cmt-change-numbers-2",
            AuthorName = "Site Manager",
            TextPreview = "Number of people changed",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = 10 });

        var (postedBookingId, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal("97011", postedBookingId);
        Assert.Contains("4 → 10", comment);
    }

    [Fact]
    public async Task ChangeNumbers_Returns400_WhenItemIdMissing()
    {
        var bookingId = await SeedBookingAsync("97012");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "", NewNumberPeople = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeNumbers_Returns400_WhenNewNumberPeopleMissing()
    {
        var bookingId = await SeedBookingAsync("97013");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "act-item-1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task ChangeNumbers_Returns400_WhenNewNumberPeopleNotPositive(int newNumberPeople)
    {
        var bookingId = await SeedBookingAsync("97014");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = newNumberPeople });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeNumbers_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/bookings/999999/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = 10 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeNumbers_Returns404_WhenItemNotInBooking()
    {
        var bookingId = await SeedBookingAsync("97015");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "item-does-not-exist", NewNumberPeople = 10 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Note: unlike add-activity/remove-activity (which call IOsmService directly), change-numbers
    // goes through IBookingMutationService.ReplaceItemsAsync — same as move-activity/change-site.
    // ReplaceItemsAsync catches all create-phase failures (including OSM auth errors) internally
    // and reports them as a RolledBack BookingActionResult (200 OK), rather than letting them
    // propagate as exceptions for the controller to map to 401/502. So there's no reachable 401
    // path here via the fake (mirrors MoveActivity/ChangeSite, which have no such test either) —
    // both an auth failure and any other create failure land on the same RolledBack assertion below.

    [Fact]
    public async Task ChangeNumbers_ReturnsRolledBack_WhenCreateFails()
    {
        var bookingId = await SeedBookingAsync("97017");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("No available slot"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/change-numbers",
            new ChangeNumbersRequest { ItemId = "act-item-1", NewNumberPeople = 10 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.RolledBack, result.Status);
    }
}
