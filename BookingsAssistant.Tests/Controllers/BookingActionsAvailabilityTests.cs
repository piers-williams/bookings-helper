using System.Net;
using System.Net.Http.Json;
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
/// Covers GET .../availability — the sole read-only action in this feature set. Unlike the
/// mutation endpoints, "not available" is a normal 200 result (Available: false), not an error;
/// only real OSM/auth/transport failures map to 401/502.
/// </summary>
public class BookingActionsAvailabilityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsAvailabilityTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_Availability_" + Guid.NewGuid();
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

    private static string Query(string activityId = "4962", string startDate = "2026-08-02", string endDate = "2026-08-02")
        => $"?activityId={activityId}&startDate={startDate}&endDate={endDate}";

    [Fact]
    public async Task CheckAvailability_Returns200Available_WhenSlotExists()
    {
        var bookingId = await SeedBookingAsync("99010");
        _fakeOsm.AvailabilityResultToReturn = new AvailabilityResult { Available = true };

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/availability{Query()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AvailabilityResult>();
        Assert.NotNull(result);
        Assert.True(result!.Available);
        Assert.Null(result.Reason);

        var call = Assert.Single(_fakeOsm.AvailabilityChecks);
        Assert.Equal("99010", call.OsmBookingId);
        Assert.Equal("4962", call.CampsiteItemId);
        Assert.Equal(new DateTime(2026, 8, 2), call.StartDate);
        Assert.Equal(new DateTime(2026, 8, 2), call.EndDate);
    }

    [Fact]
    public async Task CheckAvailability_Returns200Unavailable_WhenNoSlotExists_NotAnError()
    {
        var bookingId = await SeedBookingAsync("99011");
        _fakeOsm.AvailabilityResultToReturn = new AvailabilityResult
        {
            Available = false,
            Reason = "No available slot for 2026-08-02 to 2026-08-02"
        };

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/availability{Query()}");

        // "Not available" is still a successful query — 200, not 4xx/5xx.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AvailabilityResult>();
        Assert.NotNull(result);
        Assert.False(result!.Available);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public async Task CheckAvailability_Returns400_WhenActivityIdMissing()
    {
        var bookingId = await SeedBookingAsync("99020");

        var response = await _factory.CreateClient().GetAsync(
            $"/api/bookings/{bookingId}/availability?startDate=2026-08-02&endDate=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckAvailability_Returns400_WhenStartDateMissing()
    {
        var bookingId = await SeedBookingAsync("99021");

        var response = await _factory.CreateClient().GetAsync(
            $"/api/bookings/{bookingId}/availability?activityId=4962&endDate=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckAvailability_Returns400_WhenEndDateMissing()
    {
        var bookingId = await SeedBookingAsync("99022");

        var response = await _factory.CreateClient().GetAsync(
            $"/api/bookings/{bookingId}/availability?activityId=4962&startDate=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckAvailability_Returns400_WhenDateIsNotAValidDate()
    {
        var bookingId = await SeedBookingAsync("99023");

        var response = await _factory.CreateClient().GetAsync(
            $"/api/bookings/{bookingId}/availability?activityId=4962&startDate=not-a-date&endDate=2026-08-02");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckAvailability_Returns404_WhenBookingNotFound()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/bookings/999999/availability{Query()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckAvailability_Returns401_WhenOsmAuthFails()
    {
        var bookingId = await SeedBookingAsync("99030");
        _fakeOsm.CheckAvailabilityError = new InvalidOperationException("OSM authentication failed checking availability");

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/availability{Query()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CheckAvailability_Returns502_WhenOsmFails()
    {
        var bookingId = await SeedBookingAsync("99031");
        _fakeOsm.CheckAvailabilityError = new Exception("OSM unreachable");

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/availability{Query()}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
