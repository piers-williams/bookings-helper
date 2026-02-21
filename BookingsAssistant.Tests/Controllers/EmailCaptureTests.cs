using System.Net;
using System.Net.Http.Json;
using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingsAssistant.Tests.Controllers;

public class EmailCaptureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmailCaptureTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                var dbName = "TestDb_" + Guid.NewGuid();
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
            });
        });
    }

    [Fact]
    public async Task CaptureEmail_WithBookingRef_Returns200AndLinkedBooking()
    {
        var client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.OsmBookings.Add(new BookingsAssistant.Api.Data.Entities.OsmBooking
        {
            OsmBookingId = "99999",
            CustomerName = "Test Customer",
            CustomerEmail = "test@example.com",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(33),
            Status = "Provisional",
            LastFetched = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var request = new CaptureEmailRequest
        {
            Subject = "Query about booking #99999",
            SenderEmail = "test@example.com",
            SenderName = "Test Customer",
            BodyText = "Hi, just checking on booking #99999",
            ReceivedDate = DateTime.UtcNow
        };

        var response = await client.PostAsJsonAsync("/api/emails/capture", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        Assert.NotNull(result);
        Assert.True(result.EmailId > 0);
        Assert.Single(result.LinkedBookings);
        Assert.Equal("99999", result.LinkedBookings[0].OsmBookingId);
        Assert.True(result.AutoLinked);
    }

    [Fact]
    public async Task CaptureEmail_NoDuplicates_WhenSameEmailSentTwice()
    {
        var client = _factory.CreateClient();
        var request = new CaptureEmailRequest
        {
            Subject = "No booking ref here",
            SenderEmail = "once@example.com",
            SenderName = "Once Only",
            BodyText = "Just a plain email",
            ReceivedDate = new DateTime(2026, 2, 21, 10, 0, 0, DateTimeKind.Utc)
        };

        var first = await client.PostAsJsonAsync("/api/emails/capture", request);
        var second = await client.PostAsJsonAsync("/api/emails/capture", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var r1 = await first.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        var r2 = await second.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        Assert.Equal(r1!.EmailId, r2!.EmailId);
    }

    [Fact]
    public async Task CaptureEmail_NoBookingRef_Returns200WithEmptyLinkedBookings()
    {
        var client = _factory.CreateClient();
        var request = new CaptureEmailRequest
        {
            Subject = "General enquiry",
            SenderEmail = "visitor@example.com",
            SenderName = "A Visitor",
            BodyText = "Do you have availability in summer?",
            ReceivedDate = DateTime.UtcNow
        };

        var response = await client.PostAsJsonAsync("/api/emails/capture", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CaptureEmailResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.LinkedBookings);
        Assert.False(result.AutoLinked);
    }
}
