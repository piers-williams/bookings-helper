using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace BookingsAssistant.Tests.Controllers;

public class LinksTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LinksTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_" + Guid.NewGuid();
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
            });
        });
    }

    [Fact]
    public async Task CreateLink_WithValidIds_Returns201AndLinkDto()
    {
        // Arrange — seed an email and a booking
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var email = new EmailMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderEmailHash = "hash-sender",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ReceivedDate = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            IsRead = false,
            LastFetched = DateTime.UtcNow
        };
        db.EmailMessages.Add(email);

        var booking = new OsmBooking
        {
            OsmBookingId = "77001",
            CustomerName = "Test Group",
            StartDate = DateTime.UtcNow.AddDays(10),
            EndDate = DateTime.UtcNow.AddDays(12),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        };
        db.OsmBookings.Add(booking);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var request = new CreateLinkRequest
        {
            EmailMessageId = email.Id,
            OsmBookingId = booking.Id
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/links", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LinkDto>();
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(email.Id, result.EmailMessageId);
        Assert.Equal(booking.Id, result.OsmBookingId);
        Assert.Equal(1, result.CreatedByUserId);
    }

    [Fact]
    public async Task CreateLink_CreatesRecordInDatabase()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var email = new EmailMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            SenderEmailHash = "hash-sender-2",
            SenderName = "Another Sender",
            Subject = "Another Subject",
            ReceivedDate = new DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc),
            IsRead = false,
            LastFetched = DateTime.UtcNow
        };
        db.EmailMessages.Add(email);

        var booking = new OsmBooking
        {
            OsmBookingId = "77002",
            CustomerName = "Another Group",
            StartDate = DateTime.UtcNow.AddDays(20),
            EndDate = DateTime.UtcNow.AddDays(22),
            Status = "Future",
            LastFetched = DateTime.UtcNow
        };
        db.OsmBookings.Add(booking);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/links", new CreateLinkRequest
        {
            EmailMessageId = email.Id,
            OsmBookingId = booking.Id
        });

        // Assert the link was persisted
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var link = await verifyDb.ApplicationLinks.FirstOrDefaultAsync(
            l => l.EmailMessageId == email.Id && l.OsmBookingId == booking.Id);
        Assert.NotNull(link);
    }
}
