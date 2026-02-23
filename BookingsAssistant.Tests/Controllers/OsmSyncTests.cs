using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class OsmSyncTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm = new(); // per-instance: xUnit creates a new OsmSyncTests for each test

    public OsmSyncTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_OsmSync_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hashing:Iterations"] = "1"
                }));

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

    /// <summary>
    /// RED until Task 2 adds the <c>POST /api/bookings/sync</c> endpoint.
    /// Verifies that an explicit sync call inserts new bookings and updates existing ones.
    /// </summary>
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

        // Provisional has the existing booking (now "Confirmed" status) + new booking
        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "55002", CustomerName = "Updated Name",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Confirmed" },
            new() { OsmBookingId = "55003", CustomerName = "New Group",
                    StartDate = DateTime.UtcNow.AddDays(20), EndDate = DateTime.UtcNow.AddDays(22),
                    Status = "Provisional" }
        };

        // Confirmed list contains the same 55002 but with DIFFERENT name — dedup should prefer provisional (first)
        _fakeOsm.ConfirmedBookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "55002", CustomerName = "Confirmed Name (should be ignored)",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Confirmed" }
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

        /// <summary>
        /// When set, returned for status=="confirmed". Falls back to BookingsToReturn when null.
        /// Allows deduplication logic to be tested with distinct lists per status.
        /// </summary>
        public List<BookingDto>? ConfirmedBookingsToReturn { get; set; }

        public Task<List<BookingDto>> GetBookingsAsync(string status)
        {
            if (ConfirmedBookingsToReturn != null &&
                status.Equals("confirmed", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(ConfirmedBookingsToReturn);
            return Task.FromResult(BookingsToReturn);
        }

        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
