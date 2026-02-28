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

public class EmailListTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EmailListTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "TestDb_EmailList_" + Guid.NewGuid();
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
    public async Task GetEmails_ReturnsSeededEmails()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.EmailMessages.AddRange(
                new EmailMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderName = "Alice",
                    SenderEmailHash = "hash-alice",
                    Subject = "Booking enquiry",
                    ReceivedDate = new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc),
                    IsRead = false,
                    ExtractedBookingRef = "12345"
                },
                new EmailMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderName = "Bob",
                    SenderEmailHash = "hash-bob",
                    Subject = "General question",
                    ReceivedDate = new DateTime(2026, 2, 21, 9, 0, 0, DateTimeKind.Utc),
                    IsRead = true
                }
            );
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/emails");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EmailDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);

        // Ordered by ReceivedDate descending — Bob (Feb 21) should be first
        Assert.Equal("Bob", result.Items[0].SenderName);
        Assert.Equal("Alice", result.Items[1].SenderName);
        Assert.Equal("12345", result.Items[1].ExtractedBookingRef);
    }

    [Fact]
    public async Task GetEmails_ReturnsEmpty_WhenNoEmails()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/emails");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EmailDto>>();
        Assert.NotNull(result);
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetEmails_Pagination_ReturnsCorrectPage()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (int i = 1; i <= 5; i++)
            {
                db.EmailMessages.Add(new EmailMessage
                {
                    MessageId = Guid.NewGuid().ToString(),
                    SenderName = $"Sender {i}",
                    SenderEmailHash = $"hash-{i}",
                    Subject = $"Email {i}",
                    ReceivedDate = new DateTime(2026, 2, i, 10, 0, 0, DateTimeKind.Utc),
                    IsRead = false
                });
            }
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/emails?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<EmailDto>>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    private class NoOpOsmService : IOsmService
    {
        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());
        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));
    }
}
