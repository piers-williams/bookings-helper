using BookingsAssistant.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Services;

public class GateCodeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GateCodeService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    public GateCodeService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<GateCodeService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingBookingsAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogError(ex, "Gate code service batch failed"); }

            await Task.Delay(RunInterval, stoppingToken);
        }
    }

    public async Task ProcessPendingBookingsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var osm = scope.ServiceProvider.GetRequiredService<IOsmService>();

        var daysBefore = _configuration.GetValue("GateCode:DaysBefore", 2);
        var cutoff = DateTime.UtcNow.AddDays(daysBefore);
        var now = DateTime.UtcNow;

        // Eligibility is purely about timing and state. We do NOT require a
        // pre-resolved email here: SendBookingTemplateEmailAsync resolves the
        // recipient from the booking id itself, so a booking whose email the
        // backfill couldn't pre-resolve still gets a real send attempt (and a
        // loud failure + retry) rather than being silently excluded.
        var candidates = await context.OsmBookings
            .Where(b => b.Status == "Confirmed"
                     && b.StartDate <= cutoff
                     && b.StartDate >= now.Date
                     && b.GateCodeSentAt == null)
            .OrderBy(b => b.StartDate)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("Gate code service: no candidate bookings");
            return;
        }

        // Filter out bookings whose arrival is covered by a site duty (someone on site to let them in)
        var windowStart = candidates.Min(b => b.StartDate.Date);
        var windowEnd = candidates.Max(b => b.StartDate.Date).AddDays(1);
        var duties = await context.SiteDuties
            .Where(d => d.EndDate > windowStart && d.StartDate < windowEnd)
            .ToListAsync(ct);

        // A booking is sendable exactly when the shared evaluator says "pending".
        // The DB query above already enforces the other conditions, so the
        // evaluator only distinguishes covered-by-duty here — but routing through
        // it guarantees the dashboard's reason and the sender never disagree.
        var bookings = candidates
            .Where(b =>
            {
                var arrivalDayStart = b.StartDate.Date;
                var arrivalDayEnd = arrivalDayStart.AddDays(1);
                var coveredByDuty = duties.Any(d => d.StartDate < arrivalDayEnd && d.EndDate > arrivalDayStart);
                return GateCodeStatusEvaluator.Evaluate(
                    b.Status, b.StartDate, b.GateCodeSentAt,
                    coveredByDuty, now, daysBefore) == GateCodeStatusEvaluator.Pending;
            })
            .ToList();

        if (bookings.Count == 0)
        {
            _logger.LogDebug("Gate code service: {Count} candidates all covered by site duty", candidates.Count);
            return;
        }

        _logger.LogInformation("Gate code service: {Count} bookings need gate codes ({Covered} covered by duty)",
            bookings.Count, candidates.Count - bookings.Count);

        foreach (var booking in bookings)
        {
            try
            {
                var sent = await osm.SendBookingTemplateEmailAsync(booking.OsmBookingId);
                if (!sent)
                {
                    _logger.LogWarning("Gate code service: email send returned false for booking {Id}", booking.OsmBookingId);
                    continue;
                }

                await osm.PostCommentAsync(booking.OsmBookingId, "Gate codes sent automatically by Bookings Assistant");
                booking.GateCodeSentAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);

                _logger.LogInformation("Gate code service: sent gate codes for booking {Id} ({Name}, arriving {Date:d})",
                    booking.OsmBookingId, booking.CustomerName, booking.StartDate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gate code service: failed for booking {Id}", booking.OsmBookingId);
            }
        }
    }
}
