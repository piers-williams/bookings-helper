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

public class BookingActionsItemsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingActionsItemsTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_BookingActionsItems_" + Guid.NewGuid();
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

    [Fact]
    public async Task GetItems_Returns200WithItemsList_WhenBookingExists()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = new OsmBooking
            {
                OsmBookingId = "77001",
                CustomerName = "Scout Group Items",
                StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional"
            };
            db.OsmBookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        _fakeOsm.ItemsToReturn = new List<BookingItemDto>
        {
            new BookingItemDto
            {
                ItemId = "item-001",
                Type = "site",
                SiteId = "site-42",
                Label = "Pitch A",
                StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc)
            },
            new BookingItemDto
            {
                ItemId = "item-002",
                Type = "activity",
                ActivityId = "act-10",
                Label = "Archery Session",
                StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                StartTime = "10:00",
                EndTime = "12:00"
            }
        };

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<List<BookingItemDto>>();
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Equal("item-001", items[0].ItemId);
        Assert.Equal("site", items[0].Type);
        Assert.Equal("Pitch A", items[0].Label);
        Assert.Equal("item-002", items[1].ItemId);
        Assert.Equal("activity", items[1].Type);
        Assert.Equal("Archery Session", items[1].Label);
    }

    [Fact]
    public async Task GetItems_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/bookings/999999/items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_Returns501_WhenOsmParseNotImplemented()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = new OsmBooking
            {
                OsmBookingId = "77002",
                CustomerName = "Scout Group Unimplemented",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional"
            };
            db.OsmBookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        _fakeOsm.ThrowNotImplementedForItems = true;

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}/items");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
