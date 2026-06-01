using System.Text.Json;
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

    private async Task RunBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var osm      = scope.ServiceProvider.GetRequiredService<IOsmService>();
        var hashing  = scope.ServiceProvider.GetRequiredService<IHashingService>();

        var bookings = await context.OsmBookings
            .Where(b => b.CustomerEmailHash == null
                     && b.Status != "Past"
                     && b.Status != "Cancelled")
            .OrderBy(b => b.Id)
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
                var (fullDetails, _) = await osm.GetBookingDetailsAsync(booking.OsmBookingId);
                var email = ExtractEmail(fullDetails, _logger);

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

    internal static string? ExtractEmail(string fullDetailsJson, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(fullDetailsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(fullDetailsJson);
            var root = doc.RootElement;

            // TryGetProperty throws on non-Object elements, so every access is
            // guarded by a ValueKind check first.
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("data", out var data))
            {
                // Shape 1: { data: { contact: { email: "..." } } }
                if (data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("contact", out var contact) &&
                    contact.ValueKind == JsonValueKind.Object &&
                    contact.TryGetProperty("email", out var e1) &&
                    e1.ValueKind == JsonValueKind.String)
                    return e1.GetString();

                // Shape 2: { data: [ { label: "..email..", value: "..." } ] }
                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object &&
                            item.TryGetProperty("label", out var label) &&
                            label.GetString()?.Contains("email", StringComparison.OrdinalIgnoreCase) == true &&
                            item.TryGetProperty("value", out var val))
                            return val.GetString();
                    }
                }
            }

            logger?.LogWarning("Backfill: no email field found in response (first 300 chars): {Snippet}",
                fullDetailsJson.Length > 300 ? fullDetailsJson[..300] : fullDetailsJson);
            return null;
        }
        catch (JsonException) { return null; }
    }
}
