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
    public async Task Sync_PersistsCommentsForActiveBookings()
    {
        // Fake OSM returns one provisional and one confirmed booking, each with a comment
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

        _fakeOsm.CommentsByBookingId["77002"] = new List<CommentDto>
        {
            new() { OsmCommentId = "cmt-2", AuthorName = "Admin",
                    TextPreview = "Confirmed pitch A3", CreatedDate = DateTime.UtcNow.AddDays(-1) }
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/bookings/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.CommentsAdded);
        Assert.Equal(0, result.CommentsUpdated);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var comment1 = await db.OsmComments.FirstOrDefaultAsync(c => c.OsmCommentId == "cmt-1");
        Assert.NotNull(comment1);
        Assert.Equal("77001", comment1.OsmBookingId);
        Assert.Equal("Site Manager", comment1.AuthorName);
        Assert.Equal("Deposit received", comment1.TextPreview);
        Assert.True(comment1.IsNew);

        var comment2 = await db.OsmComments.FirstOrDefaultAsync(c => c.OsmCommentId == "cmt-2");
        Assert.NotNull(comment2);
        Assert.Equal("77002", comment2.OsmBookingId);
        Assert.Equal("Admin", comment2.AuthorName);
        Assert.Equal("Confirmed pitch A3", comment2.TextPreview);
    }

    [Fact]
    public async Task Sync_UpsertsDuplicateComments()
    {
        // First sync: inserts a comment
        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "77010", CustomerName = "Cubs Pack",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Provisional" }
        };

        _fakeOsm.CommentsByBookingId["77010"] = new List<CommentDto>
        {
            new() { OsmCommentId = "cmt-10", AuthorName = "Site Manager",
                    TextPreview = "Original text", CreatedDate = DateTime.UtcNow.AddDays(-3) }
        };

        var client = _factory.CreateClient();
        var firstResponse = await client.PostAsync("/api/bookings/sync", null);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(firstResult);
        Assert.Equal(1, firstResult.CommentsAdded);
        Assert.Equal(0, firstResult.CommentsUpdated);

        // Second sync: same OsmCommentId but updated author/text — should upsert, not duplicate
        _fakeOsm.CommentsByBookingId["77010"] = new List<CommentDto>
        {
            new() { OsmCommentId = "cmt-10", AuthorName = "Site Manager (updated)",
                    TextPreview = "Updated text", CreatedDate = DateTime.UtcNow.AddDays(-3) }
        };

        var secondResponse = await client.PostAsync("/api/bookings/sync", null);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(secondResult);
        Assert.Equal(0, secondResult.CommentsAdded);
        Assert.Equal(1, secondResult.CommentsUpdated);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Only one row in DB — no duplicates
        var allComments = await db.OsmComments.Where(c => c.OsmCommentId == "cmt-10").ToListAsync();
        Assert.Single(allComments);
        Assert.Equal("Site Manager (updated)", allComments[0].AuthorName);
        Assert.Equal("Updated text", allComments[0].TextPreview);
    }

    [Fact]
    public async Task Sync_HandlesBookingsWithNoComments()
    {
        // A booking with no comments should not cause errors
        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "77020", CustomerName = "Silent Group",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Provisional" }
        };

        // No entry in CommentsByBookingId — fake returns empty list

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/bookings/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.CommentsAdded);
        Assert.Equal(0, result.CommentsUpdated);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var commentCount = await db.OsmComments.CountAsync(c => c.OsmBookingId == "77020");
        Assert.Equal(0, commentCount);
    }

    [Fact]
    public async Task Sync_DoesNotFetchComments_ForPastOrCancelledBookings()
    {
        // Past and cancelled bookings should not have their comments fetched
        _fakeOsm.BookingsToReturn = new List<BookingDto>
        {
            new() { OsmBookingId = "77030", CustomerName = "Old Group",
                    StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(-28),
                    Status = "Past" },
            new() { OsmBookingId = "77031", CustomerName = "Cancelled Group",
                    StartDate = DateTime.UtcNow.AddDays(5), EndDate = DateTime.UtcNow.AddDays(7),
                    Status = "Cancelled" }
        };

        // These comments exist in the fake but should never be fetched
        _fakeOsm.CommentsByBookingId["77030"] = new List<CommentDto>
        {
            new() { OsmCommentId = "cmt-30", AuthorName = "Someone",
                    TextPreview = "Should not be synced", CreatedDate = DateTime.UtcNow.AddDays(-30) }
        };

        _fakeOsm.CommentsByBookingId["77031"] = new List<CommentDto>
        {
            new() { OsmCommentId = "cmt-31", AuthorName = "Someone",
                    TextPreview = "Should not be synced", CreatedDate = DateTime.UtcNow.AddDays(-5) }
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/bookings/sync", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        Assert.NotNull(result);
        Assert.Equal(0, result.CommentsAdded);
        Assert.Equal(0, result.CommentsUpdated);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var commentCount = await db.OsmComments.CountAsync();
        Assert.Equal(0, commentCount);
    }

    private class FakeOsmService : IOsmService
    {
        public List<BookingDto> BookingsToReturn { get; set; } = new();
        public Dictionary<string, List<CommentDto>> CommentsByBookingId { get; } = new();

        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(BookingsToReturn);

        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
        {
            var comments = CommentsByBookingId.TryGetValue(osmBookingId, out var list)
                ? list
                : new List<CommentDto>();
            return Task.FromResult((string.Empty, comments));
        }
    }
}
