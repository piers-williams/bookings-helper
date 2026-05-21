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

        var bookings = await context.OsmBookings
            .Where(b => b.Status == "Confirmed"
                     && b.StartDate <= cutoff
                     && b.StartDate >= now.Date
                     && b.GateCodeSentAt == null
                     && b.CustomerEmailHash != null
                     && b.CustomerEmailHash != "no-email")
            .OrderBy(b => b.StartDate)
            .ToListAsync(ct);

        if (bookings.Count == 0)
        {
            _logger.LogDebug("Gate code service: no bookings need gate codes");
            return;
        }

        _logger.LogInformation("Gate code service: {Count} bookings need gate codes", bookings.Count);

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
