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
/// Covers POST .../actions/add-activity (builds a BookingItemCreateSpec from scratch — no
/// original item to clone — via IBookingItemActionService.AddActivityAsync) and
/// GET .../available-activities (the activity catalogue, mirroring available-sites).
/// </summary>
public class BookingActionsAddActivityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsAddActivityTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_AddActivity_" + Guid.NewGuid();
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

    private static AddActivityRequest ValidRequest() => new()
    {
        ActivityId = "4962",
        StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
        StartTime = "10:00",
        EndTime = "12:00",
        NumberPeople = 8
    };

    // ── add-activity ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddActivity_Returns200WithCompletedResult_WhenHappyPath()
    {
        var bookingId = await SeedBookingAsync("98010");
        _fakeOsm.CreatedItemIds = new List<string> { "new-activity-item" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "98010",
            OsmCommentId = "cmt-add-1",
            AuthorName = "Site Manager",
            TextPreview = "audit comment",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/bookings/{bookingId}/actions/add-activity", ValidRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BookingActionResult>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);
        Assert.Equal(BookingActionStatus.Completed, result.Status);
        Assert.Contains("new-activity-item", result.Created);
        Assert.Empty(result.Deleted); // nothing to delete — this is a create-from-scratch, not a replace

        // Built the spec straight from the request — no original item involved.
        var spec = Assert.Single(_fakeOsm.CapturedSpecs);
        Assert.Equal("4962", spec.CampsiteItemId);
        Assert.Equal(8, spec.NumberPeople);
        Assert.Equal("10:00", spec.StartTime);
        Assert.Equal("12:00", spec.EndTime);
    }

    [Fact]
    public async Task AddActivity_PostsAuditComment_OnSuccess()
    {
        var bookingId = await SeedBookingAsync("98011");
        _fakeOsm.CreatedItemIds = new List<string> { "new-activity-item" };
        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "98011",
            OsmCommentId = "cmt-add-2",
            AuthorName = "Site Manager",
            TextPreview = "Added activity",
            CreatedDate = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", ValidRequest());

        var (postedBookingId, comment) = Assert.Single(_fakeOsm.CommentsPosted);
        Assert.Equal("98011", postedBookingId);
        Assert.Contains("4962", comment);
    }

    [Theory]
    [InlineData(null, "2026-08-02", "2026-08-02", 8)]
    public async Task AddActivity_Returns400_WhenActivityIdMissing(string? activityId, string startDate, string endDate, int numberPeople)
    {
        var bookingId = await SeedBookingAsync("98020");
        var request = new AddActivityRequest
        {
            ActivityId = activityId ?? string.Empty,
            StartDate = DateTime.Parse(startDate),
            EndDate = DateTime.Parse(endDate),
            NumberPeople = numberPeople
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_Returns400_WhenStartDateMissing()
    {
        var bookingId = await SeedBookingAsync("98021");
        var request = new AddActivityRequest { ActivityId = "4962", EndDate = new DateTime(2026, 8, 2), NumberPeople = 8 };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_Returns400_WhenEndDateMissing()
    {
        var bookingId = await SeedBookingAsync("98022");
        var request = new AddActivityRequest { ActivityId = "4962", StartDate = new DateTime(2026, 8, 2), NumberPeople = 8 };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_Returns400_WhenNumberPeopleMissing()
    {
        var bookingId = await SeedBookingAsync("98023");
        var request = new AddActivityRequest
        {
            ActivityId = "4962",
            StartDate = new DateTime(2026, 8, 2),
            EndDate = new DateTime(2026, 8, 2)
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddActivity_Returns400_WhenNumberPeopleNotPositive(int numberPeople)
    {
        var bookingId = await SeedBookingAsync("98024");
        var request = new AddActivityRequest
        {
            ActivityId = "4962",
            StartDate = new DateTime(2026, 8, 2),
            EndDate = new DateTime(2026, 8, 2),
            NumberPeople = numberPeople
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/bookings/999999/actions/add-activity", ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_Returns401_WhenOsmAuthFails()
    {
        var bookingId = await SeedBookingAsync("98030");
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("OSM authentication failed creating item"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddActivity_Returns502_WhenOsmCreateFails()
    {
        var bookingId = await SeedBookingAsync("98031");
        _fakeOsm.FailCreateOnCall = (1, new InvalidOperationException("No available slot for the requested window"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/actions/add-activity", ValidRequest());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    // ── available-activities ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableActivities_Returns200WithList_WhenBookingExists()
    {
        var bookingId = await SeedBookingAsync("98040");
        _fakeOsm.AvailableActivitiesToReturn = new List<AvailableSiteDto>
        {
            new() { Id = "4962", Name = "ACTIVITY - Archery" },
            new() { Id = "4961", Name = "ACTIVITY - Air Rifle Shooting" }
        };

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-activities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activities = await response.Content.ReadFromJsonAsync<List<AvailableSiteDto>>();
        Assert.NotNull(activities);
        Assert.Equal(2, activities!.Count);
        Assert.Contains(activities, a => a.Id == "4962" && a.Name == "ACTIVITY - Archery");
    }

    [Fact]
    public async Task GetAvailableActivities_Returns404_WhenBookingNotFound()
    {
        var response = await _factory.CreateClient().GetAsync("/api/bookings/999999/available-activities");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableActivities_Returns401_WhenOsmAuthFails()
    {
        var bookingId = await SeedBookingAsync("98041");
        _fakeOsm.GetActivitiesError = new InvalidOperationException("OSM authentication failed fetching activities");

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-activities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableActivities_Returns502_WhenOsmErrors()
    {
        var bookingId = await SeedBookingAsync("98042");
        _fakeOsm.GetActivitiesError = new Exception("OSM unreachable");

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-activities");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
