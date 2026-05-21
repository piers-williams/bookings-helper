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

        var candidates = await context.OsmBookings
            .Where(b => b.Status == "Confirmed"
                     && b.StartDate <= cutoff
                     && b.StartDate >= now.Date
                     && b.GateCodeSentAt == null
                     && b.CustomerEmailHash != null
                     && b.CustomerEmailHash != "no-email")
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

        // A booking is "covered" if any duty overlaps with the booking's arrival day
        var bookings = candidates
            .Where(b =>
            {
                var arrivalDayStart = b.StartDate.Date;
                var arrivalDayEnd = arrivalDayStart.AddDays(1);
                return !duties.Any(d => d.StartDate < arrivalDayEnd && d.EndDate > arrivalDayStart);
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
