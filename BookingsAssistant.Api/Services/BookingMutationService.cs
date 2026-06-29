using System.Text.Json;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Orchestrates booking item mutations: clone → create-all → delete-all with rollback.
///
/// NOTE — interim clone representation:
/// The clone payload is built from BookingItemDto (with overrides applied) serialised to JSON.
/// This works for the orchestration and override logic tests. The real raw-JSON-preserving
/// clone (which would preserve OSM-specific fields not in our DTO) will be wired up in the
/// osm-payload-mapping chunk once example response data is available.
/// </summary>
public class BookingMutationService : IBookingMutationService
{
    private readonly IOsmService _osmService;
    private readonly ILogger<BookingMutationService> _logger;

    public BookingMutationService(IOsmService osmService, ILogger<BookingMutationService> logger)
    {
        _osmService = osmService;
        _logger = logger;
    }

    public async Task<BookingActionResult> ReplaceItemsAsync(
        string osmBookingId,
        IReadOnlyList<ItemReplacement> replacements)
    {
        _logger.LogInformation(
            "ReplaceItemsAsync: booking {BookingId}, {Count} replacement(s)",
            osmBookingId, replacements.Count);

        var created = new List<string>();

        // ── PHASE 1: Create all clones ──────────────────────────────────────
        foreach (var replacement in replacements)
        {
            try
            {
                var cloneJson = BuildCloneJson(replacement);
                var newId = await _osmService.CreateBookingItemAsync(osmBookingId, cloneJson);
                created.Add(newId);
                _logger.LogInformation(
                    "ReplaceItemsAsync: created item {NewId} for booking {BookingId}",
                    newId, osmBookingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ReplaceItemsAsync: create failed for booking {BookingId}; rolling back {Count} created item(s)",
                    osmBookingId, created.Count);

                // ROLLBACK: best-effort delete everything created so far
                foreach (var rollbackId in created)
                {
                    try
                    {
                        await _osmService.DeleteBookingItemAsync(osmBookingId, rollbackId);
                        _logger.LogInformation(
                            "ReplaceItemsAsync: rollback deleted {ItemId} from booking {BookingId}",
                            rollbackId, osmBookingId);
                    }
                    catch (Exception rbEx)
                    {
                        // Swallow rollback errors — log only
                        _logger.LogWarning(rbEx,
                            "ReplaceItemsAsync: rollback delete failed for item {ItemId} on booking {BookingId}",
                            rollbackId, osmBookingId);
                    }
                }

                // Best-effort: fetch the current item list after rollback
                var itemsAfterRollback = await GetItemsSafeAsync(osmBookingId);

                return new BookingActionResult
                {
                    Status = BookingActionStatus.RolledBack,
                    Created = new List<string>(),
                    Deleted = new List<string>(),
                    Message = $"Create failed during replacement: {ex.Message}. Rolled back {created.Count} created item(s).",
                    Items = itemsAfterRollback
                };
            }
        }

        // ── PHASE 2: Delete originals (only reached when all creates succeeded) ──
        var deleted = new List<string>();
        foreach (var replacement in replacements)
        {
            var originalId = replacement.Original.ItemId;
            try
            {
                var success = await _osmService.DeleteBookingItemAsync(osmBookingId, originalId);
                if (success)
                {
                    deleted.Add(originalId);
                    _logger.LogInformation(
                        "ReplaceItemsAsync: deleted original {ItemId} from booking {BookingId}",
                        originalId, osmBookingId);
                }
                else
                {
                    _logger.LogWarning(
                        "ReplaceItemsAsync: delete returned false for original {ItemId} on booking {BookingId}",
                        originalId, osmBookingId);
                }
            }
            catch (Exception ex)
            {
                // Swallow delete failures in phase 2 — they degrade to a warning
                _logger.LogWarning(ex,
                    "ReplaceItemsAsync: delete threw for original {ItemId} on booking {BookingId}",
                    originalId, osmBookingId);
            }
        }

        var allDeleted = deleted.Count == replacements.Count;
        var status = allDeleted
            ? BookingActionStatus.Completed
            : BookingActionStatus.CompletedWithWarnings;

        var message = allDeleted
            ? $"Replaced {replacements.Count} item(s) successfully."
            : $"Created {created.Count} item(s) but only deleted {deleted.Count} of {replacements.Count} originals.";

        var freshItems = await GetItemsSafeAsync(osmBookingId);

        _logger.LogInformation(
            "ReplaceItemsAsync: {Status} for booking {BookingId} — created {C}, deleted {D}",
            status, osmBookingId, created.Count, deleted.Count);

        return new BookingActionResult
        {
            Status = status,
            Created = created,
            Deleted = deleted,
            Message = message,
            Items = freshItems
        };
    }

    /// <summary>
    /// Builds the clone JSON for a replacement by copying the original item and
    /// applying any provided overrides.
    ///
    /// INTERIM CLONE REPRESENTATION: this serialises BookingItemDto with overrides.
    /// The real raw-JSON-preserving clone (preserving unmapped OSM fields) is deferred
    /// to the osm-payload-mapping chunk pending example response data.
    /// </summary>
    private static string BuildCloneJson(ItemReplacement replacement)
    {
        var original = replacement.Original;

        // Copy the original item — note ItemId is intentionally excluded from
        // the clone payload (OSM will assign a new one on creation).
        var clone = new BookingItemDto
        {
            ItemId = string.Empty,   // excluded from payload; OSM assigns on create
            Type = original.Type,
            SiteId = replacement.NewSiteId ?? original.SiteId,
            ActivityId = original.ActivityId,
            Label = original.Label,
            StartDate = replacement.NewStartDate ?? original.StartDate,
            EndDate = replacement.NewEndDate ?? original.EndDate,
            StartTime = replacement.NewStartTime ?? original.StartTime,
            EndTime = replacement.NewEndTime ?? original.EndTime
        };

        return JsonSerializer.Serialize(clone);
    }

    private async Task<List<BookingItemDto>> GetItemsSafeAsync(string osmBookingId)
    {
        try
        {
            return await _osmService.GetBookingItemsAsync(osmBookingId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ReplaceItemsAsync: could not fetch items after operation for booking {BookingId}",
                osmBookingId);
            return new List<BookingItemDto>();
        }
    }
}
