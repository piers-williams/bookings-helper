using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Services;
using BookingsAssistant.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookingsAssistant.Tests.Services;

public class BookingDetailBackfillServiceTests
{
    private static (ServiceProvider provider, FakeOsmService osm) CreateServices()
    {
        var services = new ServiceCollection();
        var dbName = "TestDb_Backfill_" + Guid.NewGuid();
        services.AddDbContext<ApplicationDbContext>(o =>
            o.UseInMemoryDatabase(dbName));

        var osm = new FakeOsmService();
        services.AddSingleton<IOsmService>(osm);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Hashing:Iterations"] = "1",
                // Non-existent directory forces the deterministic dev-fallback secret.
                ["Hashing:SecretPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "secret.txt")
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddSingleton<IHashingService, HashingService>();

        return (services.BuildServiceProvider(), osm);
    }

    private static BookingDetailBackfillService CreateService(IServiceProvider provider)
        => new(provider.GetRequiredService<IServiceScopeFactory>(),
               NullLogger<BookingDetailBackfillService>.Instance);

    [Fact]
    public async Task RunBatch_HashesResolvedContactEmail()
    {
        var (provider, osm) = CreateServices();
        var today = DateTime.UtcNow.Date;
        osm.ContactEmailByBookingId["10"] = "scout@example.com";

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "10", CustomerName = "Group", Status = "Confirmed",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3)
            });
            await db.SaveChangesAsync();
        }

        await CreateService(provider).RunBatchAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.OsmBookings.SingleAsync(b => b.OsmBookingId == "10");
            Assert.NotNull(booking.CustomerEmailHash);
            Assert.NotEqual("no-email", booking.CustomerEmailHash);
        }
    }

    [Fact]
    public async Task RunBatch_SetsNoEmailSentinel_WhenNoContactEmailResolves()
    {
        var (provider, _) = CreateServices();
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "20", CustomerName = "Group", Status = "Confirmed",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3)
            });
            await db.SaveChangesAsync();
        }

        await CreateService(provider).RunBatchAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var booking = await db.OsmBookings.SingleAsync(b => b.OsmBookingId == "20");
            Assert.Equal("no-email", booking.CustomerEmailHash);
        }
    }

    // Regression: a booking arriving imminently must be resolved ahead of a
    // backlog of far-future bookings, even though it was inserted last (highest
    // Id). Ordering by Id would leave it beyond the batch cut-off, unresolved.
    [Fact]
    public async Task RunBatch_PrioritisesImminentArrival_OverOlderFarFutureBacklog()
    {
        var (provider, osm) = CreateServices();
        var today = DateTime.UtcNow.Date;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // 20 far-future bookings inserted first → lowest Ids, fill a whole batch.
            for (var i = 0; i < 20; i++)
            {
                db.OsmBookings.Add(new OsmBooking
                {
                    OsmBookingId = $"far{i}", CustomerName = "Far Group", Status = "Confirmed",
                    StartDate = today.AddDays(100 + i), EndDate = today.AddDays(102 + i)
                });
            }
            // The imminent booking is inserted last → highest Id.
            db.OsmBookings.Add(new OsmBooking
            {
                OsmBookingId = "imminent", CustomerName = "Tomorrow Group", Status = "Confirmed",
                StartDate = today.AddDays(1), EndDate = today.AddDays(3)
            });
            await db.SaveChangesAsync();
        }
        osm.ContactEmailByBookingId["imminent"] = "tomorrow@example.com";

        await CreateService(provider).RunBatchAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var imminent = await db.OsmBookings.SingleAsync(b => b.OsmBookingId == "imminent");
            Assert.NotNull(imminent.CustomerEmailHash);
            Assert.NotEqual("no-email", imminent.CustomerEmailHash);
        }
    }
}
