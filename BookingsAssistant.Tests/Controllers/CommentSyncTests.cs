using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class CommentSyncTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm = new();

    public CommentSyncTests(WebApplicationFactory<Program> factory)
    {
        var dbName = Guid.NewGuid().ToString(); // OUTSIDE the lambda — ensures one DB per test instance
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
    public async Task Sync_DoesNotFetchOrPersistComments()
    {
        // Fake OSM has comments configured for active bookings, but bulk sync should
        // never fetch or persist them — comment sync moved to the booking-detail endpoint.
        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "77001", CustomerName = "Scout Troop A",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Provisional" },
            new() { OsmBookingId = "77002", CustomerName = "Scout Troop B",
                    StartDate = DateTime.UtcNow.AddDays(10), EndDate = DateTime.UtcNow.AddDays(12),
                    Status = "Confirmed" }
        };

        _fakeOsm.CommentsByBookingId["77001"] = new List<CommentDto>
        {
            new() { OsmCommentId = "cmt-1", AuthorName = "Site Manager",
                    TextPreview = "Deposit received", CreatedDate = DateTime.UtcNow.AddDays(-2) }
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/bookings/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Added);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var commentCount = await db.OsmComments.CountAsync();
        Assert.Equal(0, commentCount);
    }
}
