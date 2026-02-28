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

public class CommentListTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CommentListTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_CommentList_" + Guid.NewGuid(); // OUTSIDE the lambda — ensures one DB per test instance
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
                services.AddSingleton<IOsmService>(new NoOpOsmService());
            });
        });
    }

    [Fact]
    public async Task GetComments_ReturnsFromDatabase()
    {
        var booking = new OsmBooking
        {
            OsmBookingId = "88001",
            CustomerName = "Scout Group Alpha",
            StartDate = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
            Status = "Provisional"
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(booking);
            db.OsmComments.Add(new OsmComment
            {
                OsmBookingId = "88001",
                OsmCommentId = "cmt-88001",
                AuthorName = "Site Manager",
                TextPreview = "Pitch assigned",
                CreatedDate = new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc),
                IsNew = true
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        Assert.NotNull(result);
        Assert.Single(result);

        var comment = result[0];
        Assert.Equal("cmt-88001", comment.OsmCommentId);
        Assert.Equal("Site Manager", comment.AuthorName);
        Assert.Equal("Pitch assigned", comment.TextPreview);
        Assert.True(comment.IsNew);

        // Booking context should be included
        Assert.NotNull(comment.Booking);
        Assert.Equal("88001", comment.Booking.OsmBookingId);
        Assert.Equal("Scout Group Alpha", comment.Booking.CustomerName);
        Assert.Equal("Provisional", comment.Booking.Status);
    }

    [Fact]
    public async Task GetComments_FiltersByNewOnly()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "88010",
                CustomerName = "Group B",
                StartDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Confirmed"
            });
            db.OsmComments.AddRange(
                new OsmComment
                {
                    OsmBookingId = "88010",
                    OsmCommentId = "cmt-new-1",
                    AuthorName = "Admin",
                    TextPreview = "New comment",
                    CreatedDate = new DateTime(2026, 2, 25, 10, 0, 0, DateTimeKind.Utc),
                    IsNew = true
                },
                new OsmComment
                {
                    OsmBookingId = "88010",
                    OsmCommentId = "cmt-old-1",
                    AuthorName = "Admin",
                    TextPreview = "Old comment",
                    CreatedDate = new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc),
                    IsNew = false
                }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/comments?newOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("cmt-new-1", result[0].OsmCommentId);
        Assert.True(result[0].IsNew);
    }

    [Fact]
    public async Task GetComments_OrdersByDateDescending()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "88020",
                CustomerName = "Group C",
                StartDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional"
            });
            db.OsmComments.AddRange(
                new OsmComment
                {
                    OsmBookingId = "88020",
                    OsmCommentId = "cmt-oldest",
                    AuthorName = "Admin",
                    TextPreview = "Oldest",
                    CreatedDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsNew = false
                },
                new OsmComment
                {
                    OsmBookingId = "88020",
                    OsmCommentId = "cmt-newest",
                    AuthorName = "Admin",
                    TextPreview = "Newest",
                    CreatedDate = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
                    IsNew = true
                },
                new OsmComment
                {
                    OsmBookingId = "88020",
                    OsmCommentId = "cmt-middle",
                    AuthorName = "Admin",
                    TextPreview = "Middle",
                    CreatedDate = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                    IsNew = true
                }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // Should be ordered newest first
        Assert.Equal("cmt-newest", result[0].OsmCommentId);
        Assert.Equal("cmt-middle", result[1].OsmCommentId);
        Assert.Equal("cmt-oldest", result[2].OsmCommentId);
    }

    [Fact]
    public async Task GetComments_RespectsLimit()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "88030",
                CustomerName = "Group D",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Confirmed"
            });
            for (int i = 1; i <= 5; i++)
            {
                db.OsmComments.Add(new OsmComment
                {
                    OsmBookingId = "88030",
                    OsmCommentId = $"cmt-limit-{i}",
                    AuthorName = "Admin",
                    TextPreview = $"Comment {i}",
                    CreatedDate = new DateTime(2026, 2, i, 10, 0, 0, DateTimeKind.Utc),
                    IsNew = true
                });
            }
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/comments?limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetComments_ReturnsEmpty_WhenNoComments()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CommentDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    private class NoOpOsmService : IOsmService
    {
        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());
        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
