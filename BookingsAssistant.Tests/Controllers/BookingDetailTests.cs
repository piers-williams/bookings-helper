using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsAssistant.Tests.Controllers;

public class BookingDetailTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FakeOsmService _fakeOsm;

    public BookingDetailTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_BookingDetail_" + Guid.NewGuid();
        _fakeOsm = new FakeOsmService();
        _factory = factory.WithWebHostBuilder(builder =>
        {
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
    public async Task GetById_ReturnsBooking_WithComments()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = new OsmBooking
            {
                OsmBookingId = "55001",
                CustomerName = "Test Scout Group",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional",
                LastFetched = DateTime.UtcNow
            };
            db.OsmBookings.Add(booking);

            var comment = new OsmComment
            {
                OsmBookingId = "55001",
                OsmCommentId = "cmt-001",
                AuthorName = "Site Manager",
                TextPreview = "Confirmed pitch allocation",
                CreatedDate = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc),
                IsNew = false
            };
            db.OsmComments.Add(comment);

            await db.SaveChangesAsync();

            bookingId = booking.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.NotNull(detail);

        Assert.Equal(bookingId, detail.Id);
        Assert.Equal("55001", detail.OsmBookingId);
        Assert.Equal("Test Scout Group", detail.CustomerName);
        Assert.Equal("Provisional", detail.Status);

        Assert.Single(detail.Comments);
        Assert.Equal("Site Manager", detail.Comments[0].AuthorName);
        Assert.Equal("Confirmed pitch allocation", detail.Comments[0].TextPreview);
        Assert.Equal("55001", detail.Comments[0].OsmBookingId);
    }

    [Fact]
    public async Task GetById_Returns404_WhenBookingNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/bookings/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsEmptyComments_WhenNoneExist()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = new OsmBooking
            {
                OsmBookingId = "55002",
                CustomerName = "Lonely Scout Group",
                StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Future",
                LastFetched = DateTime.UtcNow
            };
            db.OsmBookings.Add(booking);
            await db.SaveChangesAsync();

            bookingId = booking.Id;
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.NotNull(detail);

        Assert.Equal(bookingId, detail.Id);
        Assert.Equal("55002", detail.OsmBookingId);
        Assert.Equal("Lonely Scout Group", detail.CustomerName);
        Assert.Empty(detail.Comments);
    }

    [Fact]
    public async Task GetById_AddsCommentThatOnlyExistsInOsm()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = new OsmBooking
            {
                OsmBookingId = "55003",
                CustomerName = "Live Fetch Group",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional",
                LastFetched = DateTime.UtcNow
            };
            db.OsmBookings.Add(booking);
            await db.SaveChangesAsync();

            bookingId = booking.Id;
        }

        _fakeOsm.CommentsByBookingId["55003"] = new List<CommentDto>
        {
            new CommentDto
            {
                OsmBookingId = "55003",
                OsmCommentId = "cmt-live-1",
                AuthorName = "Warden",
                TextPreview = "Arriving late on Friday",
                CreatedDate = new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Utc),
                IsNew = false
            }
        };

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.NotNull(detail);

        Assert.Single(detail.Comments);
        Assert.Equal("cmt-live-1", detail.Comments[0].OsmCommentId);
        Assert.Equal("Warden", detail.Comments[0].AuthorName);
        Assert.Equal("Arriving late on Friday", detail.Comments[0].TextPreview);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.OsmComments.SingleAsync(c => c.OsmCommentId == "cmt-live-1");
            Assert.Equal("55003", stored.OsmBookingId);
        }
    }

    [Fact]
    public async Task GetById_UpdatesExistingCommentText_WhenOsmHasNewerText()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = new OsmBooking
            {
                OsmBookingId = "55004",
                CustomerName = "Update Group",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional",
                LastFetched = DateTime.UtcNow
            };
            db.OsmBookings.Add(booking);

            db.OsmComments.Add(new OsmComment
            {
                OsmBookingId = "55004",
                OsmCommentId = "cmt-002",
                AuthorName = "Site Manager",
                TextPreview = "Old text",
                CreatedDate = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc),
                IsNew = false
            });

            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        _fakeOsm.CommentsByBookingId["55004"] = new List<CommentDto>
        {
            new CommentDto
            {
                OsmBookingId = "55004",
                OsmCommentId = "cmt-002",
                AuthorName = "Site Manager",
                TextPreview = "Updated text from OSM",
                CreatedDate = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc),
                IsNew = false
            }
        };

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.NotNull(detail);

        Assert.Single(detail.Comments);
        Assert.Equal("cmt-002", detail.Comments[0].OsmCommentId);
        Assert.Equal("Updated text from OSM", detail.Comments[0].TextPreview);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var count = await db.OsmComments.CountAsync(c => c.OsmBookingId == "55004");
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task GetById_KeepsExistingComments_WhenOsmFetchReturnsNone()
    {
        int bookingId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = new OsmBooking
            {
                OsmBookingId = "55005",
                CustomerName = "Failed Fetch Group",
                StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                Status = "Provisional",
                LastFetched = DateTime.UtcNow
            };
            db.OsmBookings.Add(booking);

            db.OsmComments.Add(new OsmComment
            {
                OsmBookingId = "55005",
                OsmCommentId = "cmt-003",
                AuthorName = "Site Manager",
                TextPreview = "Existing comment untouched",
                CreatedDate = new DateTime(2026, 5, 10, 9, 0, 0, DateTimeKind.Utc),
                IsNew = false
            });

            await db.SaveChangesAsync();
            bookingId = booking.Id;
        }

        // No entry configured in _fakeOsm.CommentsByBookingId for "55005",
        // so GetBookingCommentsAsync returns an empty list, simulating a
        // failed or empty fetch from OSM.

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<BookingDetailDto>();
        Assert.NotNull(detail);

        Assert.Single(detail.Comments);
        Assert.Equal("cmt-003", detail.Comments[0].OsmCommentId);
        Assert.Equal("Existing comment untouched", detail.Comments[0].TextPreview);
    }
}
