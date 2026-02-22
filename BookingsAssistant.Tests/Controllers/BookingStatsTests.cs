using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class BookingStatsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookingStatsTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_Stats_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // Prevent startup sync from calling real OSM
                services.RemoveAll<IOsmService>();
                services.AddSingleton<IOsmService>(new NoOpOsmService());
            });
        });
    }

    [Fact]
    public async Task GetStats_ReturnsCorrectCounts()
    {
        var today = DateTime.UtcNow.Date;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.AddRange(
                // On site now: Confirmed, started yesterday, ends tomorrow
                new OsmBooking { OsmBookingId = "s1", CustomerName = "On Site Now",
                    Status = "Confirmed",
                    StartDate = today.AddDays(-1), EndDate = today.AddDays(1),
                    LastFetched = DateTime.UtcNow },
                // Arriving this week: Future, starts in 3 days
                new OsmBooking { OsmBookingId = "s2", CustomerName = "Arriving Week",
                    Status = "Future",
                    StartDate = today.AddDays(3), EndDate = today.AddDays(5),
                    LastFetched = DateTime.UtcNow },
                // Arriving next 30 days but NOT this week: Future, starts in 20 days
                new OsmBooking { OsmBookingId = "s3", CustomerName = "Arriving Month",
                    Status = "Future",
                    StartDate = today.AddDays(20), EndDate = today.AddDays(22),
                    LastFetched = DateTime.UtcNow },
                // Provisional: should count in Provisional only (starts in 40 days, out of 30-day window)
                new OsmBooking { OsmBookingId = "s4", CustomerName = "Provisional Group",
                    Status = "Provisional",
                    StartDate = today.AddDays(40), EndDate = today.AddDays(42),
                    LastFetched = DateTime.UtcNow },
                // Cancelled: must NOT appear in any arriving count
                new OsmBooking { OsmBookingId = "s5", CustomerName = "Cancelled Group",
                    Status = "Cancelled",
                    StartDate = today.AddDays(2), EndDate = today.AddDays(4),
                    LastFetched = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/bookings/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<BookingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats.OnSiteNow);
        Assert.Equal(1, stats.ArrivingThisWeek);
        Assert.Equal(2, stats.ArrivingNext30Days); // s2 (3 days) and s3 (20 days)
        Assert.Equal(1, stats.Provisional);
        Assert.NotNull(stats.LastSynced);
    }

    private class NoOpOsmService : IOsmService
    {
        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());
        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
