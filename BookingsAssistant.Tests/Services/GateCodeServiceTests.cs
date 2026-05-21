using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

public class GateCodeServiceTests
{
    private static (ServiceProvider provider, string dbName) CreateServices(FakeOsmService? fakeOsm = null, int daysBefore = 2)
    {
        var dbName = "TestDb_GateCode_" + Guid.NewGuid();
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var osm = fakeOsm ?? new FakeOsmService();
        services.AddSingleton<IOsmService>(osm);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GateCode:DaysBefore"] = daysBefore.ToString()
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        return (services.BuildServiceProvider(), dbName);
    }

    private static GateCodeService CreateService(IServiceProvider provider)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var config = provider.GetRequiredService<IConfiguration>();
        return new GateCodeService(scopeFactory, config, NullLogger<GateCodeService>.Instance);
    }

    [Fact]
    public async Task SendsGateCode_ForConfirmedBookingArrivingWithinThreshold()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "100", CustomerName = "Test Group",
                Status = "Confirmed",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Single(fakeOsm.EmailsSent);
        Assert.Equal("100", fakeOsm.EmailsSent[0]);
        Assert.Contains(fakeOsm.CommentsPosted, c => c.bookingId == "100");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.OsmBookings.SingleAsync(b => b.OsmBookingId == "100");
            Assert.NotNull(booking.GateCodeSentAt);
        }
    }

    [Fact]
    public async Task SkipsBooking_WhenTooFarAway()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "200", CustomerName = "Far Away Group",
                Status = "Confirmed",
                StartDate = today.AddDays(5), EndDate = today.AddDays(7),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Empty(fakeOsm.EmailsSent);
    }

    [Fact]
    public async Task SkipsBooking_WhenAlreadySent()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "300", CustomerName = "Already Sent Group",
                Status = "Confirmed",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = DateTime.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Empty(fakeOsm.EmailsSent);
    }

    [Fact]
    public async Task SkipsBooking_WhenCancelled()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "400", CustomerName = "Cancelled Group",
                Status = "Cancelled",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Empty(fakeOsm.EmailsSent);
    }

    [Fact]
    public async Task SkipsBooking_WhenNoEmail()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.AddRange(
                new OsmBooking
                {
                    OsmBookingId = "500", CustomerName = "No Email Group",
                    Status = "Confirmed",
                    StartDate = today.AddDays(1), EndDate = today.AddDays(3),
                    CustomerEmailHash = "no-email",
                    GateCodeSentAt = null
                },
                new OsmBooking
                {
                    OsmBookingId = "501", CustomerName = "Null Email Group",
                    Status = "Confirmed",
                    StartDate = today.AddDays(1), EndDate = today.AddDays(3),
                    CustomerEmailHash = null,
                    GateCodeSentAt = null
                });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Empty(fakeOsm.EmailsSent);
    }

    [Fact]
    public async Task SkipsBooking_WhenStartDateAlreadyPassed()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "600", CustomerName = "Past Start Group",
                Status = "Confirmed",
                StartDate = today.AddDays(-1), EndDate = today.AddDays(1),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Empty(fakeOsm.EmailsSent);
    }

    [Fact]
    public async Task DoesNotMarkSent_WhenEmailSendFails()
    {
        var fakeOsm = new FakeOsmService { ShouldFailSend = true };
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "700", CustomerName = "Fail Group",
                Status = "Confirmed",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.OsmBookings.SingleAsync(b => b.OsmBookingId == "700");
            Assert.Null(booking.GateCodeSentAt);
        }
    }

    [Fact]
    public async Task RespectsConfigurableDaysBefore()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm, daysBefore: 5);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "800", CustomerName = "Five Day Group",
                Status = "Confirmed",
                StartDate = today.AddDays(4), EndDate = today.AddDays(6),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Single(fakeOsm.EmailsSent);
        Assert.Equal("800", fakeOsm.EmailsSent[0]);
    }

    [Fact]
    public async Task SendsToday_ForBookingStartingToday()
    {
        var fakeOsm = new FakeOsmService();
        var (provider, _) = CreateServices(fakeOsm);
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "900", CustomerName = "Today Group",
                Status = "Confirmed",
                StartDate = today, EndDate = today.AddDays(2),
                CustomerEmailHash = "abc123",
                GateCodeSentAt = null
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService(provider);
        await service.ProcessPendingBookingsAsync(CancellationToken.None);

        Assert.Single(fakeOsm.EmailsSent);
    }

    private class FakeOsmService : IOsmService
    {
        public List<string> EmailsSent { get; } = new();
        public List<(string bookingId, string comment)> CommentsPosted { get; } = new();
        public bool ShouldFailSend { get; set; }

        public Task<List<BookingDto>> GetBookingsAsync(string status)
            => Task.FromResult(new List<BookingDto>());

        public Task<(string FullDetails, List<CommentDto> Comments)> GetBookingDetailsAsync(string osmBookingId)
            => Task.FromResult((string.Empty, new List<CommentDto>()));

        public Task<CommentDto?> PostCommentAsync(string osmBookingId, string comment)
        {
            CommentsPosted.Add((osmBookingId, comment));
            return Task.FromResult<CommentDto?>(null);
        }

        public Task<bool> SendBookingTemplateEmailAsync(string osmBookingId)
        {
            if (ShouldFailSend) return Task.FromResult(false);
            EmailsSent.Add(osmBookingId);
            return Task.FromResult(true);
        }
    }
}
