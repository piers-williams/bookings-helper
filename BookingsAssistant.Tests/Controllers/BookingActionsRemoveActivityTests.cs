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
/// Covers POST .../actions/remove-activity — a standalone hard delete of an existing item
/// (site or activity) via IBookingItemActionService.RemoveActivityAsync. Unlike
/// MoveActivity/ChangeSite (clone-then-delete-original), there is no create step here, so
/// there's no rolled-back state — only Completed (deleted) or Failed (not deleted).
/// </summary>
public class BookingActionsRemoveActivityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsRemoveActivityTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_RemoveActivity_" + Guid.NewGuid();
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
        EndTime = "12:00"
    };

    [Fact]
    public async Task RemoveActivity_Returns200WithCompletedResult_WhenHappyPath()
    {
        var bookingId = await SeedBookingAsync("98110");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "98110",
            OsmCommentId = "cmt-remove-1",
            AuthorName = "Site Manager",
            TextPreview = "Removed 'Archery Session'.",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Contains("act-item-1", result.Deleted);
        Assert.Empty(result.Created); // nothing created — this is a straight delete, not a replace

        Assert.Contains(("98110", "act-item-1"), _fakeOsm.DeletedItems);
    }

    [Fact]
    public async Task RemoveActivity_PostsAuditComment_OnSuccess()
    {
        var bookingId = await SeedBookingAsync("98111");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "98111",
            OsmCommentId = "cmt-remove-2",
            AuthorName = "Site Manager",
            TextPreview = "Removed 'Archery Session'.",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        var (postedBookingId, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal("98111", postedBookingId);
        Assert.Contains("Archery Session", comment);
    }

    [Fact]
    public async Task RemoveActivity_Returns400_WhenItemIdMissing()
    {
        var bookingId = await SeedBookingAsync("98112");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveActivity_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/bookings/999999/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveActivity_Returns404_WhenItemNotInBooking()
    {
        var bookingId = await SeedBookingAsync("98113");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "item-does-not-exist" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveActivity_Returns401_WhenOsmAuthFails()
    {
        var bookingId = await SeedBookingAsync("98114");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.DeleteThrowForIds.Add("act-item-1");
        _fakeOsm.DeleteThrowException = new InvalidOperationException("OSM authentication failed deleting item");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemoveActivity_Returns502_WhenOsmDeleteThrows()
    {
        var bookingId = await SeedBookingAsync("98115");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.DeleteThrowForIds.Add("act-item-1");
        _fakeOsm.DeleteThrowException = new InvalidOperationException("Item is locked and cannot be deleted");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task RemoveActivity_ReturnsFailedStatus_WhenOsmDeleteReturnsFalse()
    {
        var bookingId = await SeedBookingAsync("98116");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.DeleteReturnFalseForIds.Add("act-item-1");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        // A delete that OSM simply refuses (returns false, not an exception) is a legitimate
        // engine outcome — not a transport failure — so it's 200 with a Failed status, per the
        // controller's "200 OK for all engine outcomes" convention.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Failed, result.Status);
        Assert.Empty(result.Deleted);
    }

    [Fact]
    public async Task RemoveActivity_DoesNotPostAuditComment_WhenOsmDeleteReturnsFalse()
    {
        var bookingId = await SeedBookingAsync("98117");
        _fakeOsm.ItemsToReturn = new List<BookingItemDto> { MakeActivityItem("act-item-1") };
        _fakeOsm.DeleteReturnFalseForIds.Add("act-item-1");

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/remove-activity",
            new RemoveActivityRequest { ItemId = "act-item-1" });

        Assert.Empty(_fakeOsm.CommentsPosted);
    }
}
