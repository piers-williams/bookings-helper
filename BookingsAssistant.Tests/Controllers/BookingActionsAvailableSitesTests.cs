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

public class BookingActionsAvailableSitesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsAvailableSitesTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_AvailableSites_" + Guid.NewGuid();
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

    [Fact]
    public async Task GetAvailableSites_Returns200WithList_WhenBookingExists()
    {
        var bookingId = await SeedBookingAsync("88001");
        _fakeOsm.AvailableSitesToReturn = new List<AvailableSiteDto>
        {
            new() { Id = "1387", Name = "Hayvern" },
            new() { Id = "1404", Name = "Birch" }
        };

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-sites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sites = await response.Content.ReadFromJsonAsync<List<AvailableSiteDto>>();
        Assert.NotNull(sites);
        Assert.Equal(2, sites!.Count);
        Assert.Contains(sites, s => s.Id == "1387" && s.Name == "Hayvern");
    }

    [Fact]
    public async Task GetAvailableSites_Returns404_WhenBookingNotFound()
    {
        var response = await _factory.CreateClient().GetAsync("/api/bookings/999999/available-sites");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableSites_Returns200WithEmptyList_WhenNoSites()
    {
        var bookingId = await SeedBookingAsync("88002");
        _fakeOsm.AvailableSitesToReturn = new List<AvailableSiteDto>();

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-sites");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sites = await response.Content.ReadFromJsonAsync<List<AvailableSiteDto>>();
        Assert.NotNull(sites);
        Assert.Empty(sites!);
    }

    [Fact]
    public async Task GetAvailableSites_Returns401_WhenOsmAuthFails()
    {
        var bookingId = await SeedBookingAsync("88003");
        _fakeOsm.GetSitesError = new InvalidOperationException("OSM authentication failed fetching sites");

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-sites");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableSites_Returns502_WhenOsmErrors()
    {
        var bookingId = await SeedBookingAsync("88004");
        _fakeOsm.GetSitesError = new Exception("OSM unreachable");

        var response = await _factory.CreateClient().GetAsync($"/api/bookings/{bookingId}/available-sites");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
