using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class CommentPostTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public CommentPostTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_CommentPost_" + Guid.NewGuid();
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
    public async Task PostComment_Success_ReturnsCommentAndPersists()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = new OsmBooking
            {
                OsmBookingId = "99001",
                CustomerName = "Scout Group Alpha",
                StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional"
            };
            db.OsmBookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        _fakeOsm.CommentToReturn = new CommentDto
        {
            OsmBookingId = "99001",
            OsmCommentId = "cmt-new-99001",
            AuthorName = "Site Manager",
            TextPreview = "Pitch confirmed",
            CreatedDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            IsNew = false
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/comments",
            new { comment = "Pitch confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CommentDto>();
        Assert.NotNull(result);
        Assert.Equal("cmt-new-99001", result.OsmCommentId);
        Assert.Equal("Site Manager", result.AuthorName);
        Assert.Equal("Pitch confirmed", result.TextPreview);
        Assert.True(result.Id > 0, "Returned DTO should have a DB-assigned Id");

        // Verify persisted to DB
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db2.OsmComments.FirstOrDefaultAsync(c => c.OsmCommentId == "cmt-new-99001");
        Assert.NotNull(persisted);
        Assert.Equal("99001", persisted.OsmBookingId);
        Assert.Equal("Site Manager", persisted.AuthorName);
        Assert.Equal("Pitch confirmed", persisted.TextPreview);
    }

    [Fact]
    public async Task PostComment_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/bookings/999999/comments",
            new { comment = "This booking does not exist" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostComment_Returns502_WhenOsmFails()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = new OsmBooking
            {
                OsmBookingId = "99002",
                CustomerName = "Scout Group Beta",
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional"
            };
            db.OsmBookings.Add(booking);
            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        // Leave CommentToReturn as null so OSM returns failure
        _fakeOsm.CommentToReturn = null;

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/bookings/{bookingId}/comments",
            new { comment = "This will fail at OSM" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private class FakeOsmService : IOsmService
    {
        public CommentDto? CommentToReturn { get; set; }

        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());

        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));

        public Task<CommentDto?> PostCommentAsync(string osmBookingId, string comment)
            => Task.FromResult(CommentToReturn);
    }
}
