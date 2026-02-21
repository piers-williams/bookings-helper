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
