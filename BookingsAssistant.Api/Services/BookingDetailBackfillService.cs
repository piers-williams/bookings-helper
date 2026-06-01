using BookingsAssistant.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

public class BookingDetailBackfillService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingDetailBackfillService> _logger;
    private const int BatchSize = 20;
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(30);

    public BookingDetailBackfillService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingDetailBackfillService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before first run
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunBatchAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "Backfill batch failed"); }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    public async Task RunBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var osm      = scope.ServiceProvider.GetRequiredService<IOsmService>();
        var hashing  = scope.ServiceProvider.GetRequiredService<IHashingService>();

        // Process soonest-arriving bookings first. A code is needed before
        // arrival, so an imminent booking must resolve its email ahead of the
        // backlog — ordering by Id (insertion order) would leave new bookings
        // at the back of the queue and risk missing their gate-code window.
        var today = DateTime.UtcNow.Date;
        var bookings = await context.OsmBookings
            .Where(b => b.CustomerEmailHash == null
                     && b.Status != "Past"
                     && b.Status != "Cancelled")
            .OrderByDescending(b => b.StartDate >= today)
            .ThenBy(b => b.StartDate)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (bookings.Count == 0)
        {
            _logger.LogDebug("Backfill: nothing to process");
            return;
        }

        _logger.LogInformation("Backfill: processing {Count} bookings", bookings.Count);

        foreach (var booking in bookings)
        {
            try
            {
                var email = await osm.GetBookingContactEmailAsync(booking.OsmBookingId);

                // "no-email" sentinel prevents retrying bookings that genuinely have no email
                booking.CustomerEmailHash = email != null
                    ? hashing.HashValue(email)
                    : "no-email";

                if (booking.CustomerNameHash == null)
                    booking.CustomerNameHash = hashing.HashValue(booking.CustomerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backfill: failed for booking {Id}", booking.OsmBookingId);
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
