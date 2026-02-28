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

public class BookingDetailTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookingDetailTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_BookingDetail_" + Guid.NewGuid();
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
    public async Task GetById_ReturnsBooking_WithLinkedEmailsAndComments()
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

            var email = new EmailMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                SenderName = "Scout Leader",
                SenderEmailHash = "hash-scout",
                Subject = "Booking #55001 enquiry",
                ReceivedDate = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc),
                IsRead = false,
                ExtractedBookingRef = "55001"
            };
            db.EmailMessages.Add(email);

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

            // Link the email to the booking
            db.ApplicationLinks.Add(new ApplicationLink
            {
                EmailMessageId = email.Id,
                OsmBookingId = booking.Id,
                CreatedDate = DateTime.UtcNow
            });
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

        Assert.Single(detail.LinkedEmails);
        Assert.Equal("Scout Leader", detail.LinkedEmails[0].SenderName);
        Assert.Equal("Booking #55001 enquiry", detail.LinkedEmails[0].Subject);
        Assert.Equal("55001", detail.LinkedEmails[0].ExtractedBookingRef);

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
    public async Task GetById_ReturnsEmptyCollections_WhenNoLinksOrComments()
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
        Assert.Empty(detail.LinkedEmails);
        Assert.Empty(detail.Comments);
    }

    private class NoOpOsmService : IOsmService
    {
        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());
        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
