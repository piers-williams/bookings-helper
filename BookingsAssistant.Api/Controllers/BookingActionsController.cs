using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingActionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    // Used to resolve booking items by id before delegating the mutation to the engine.
    private readonly IOsmService _osmService;
    private readonly IBookingMutationService _mutationService;
    private readonly ILogger<BookingActionsController> _logger;

    public BookingActionsController(
        ApplicationDbContext context,
        IOsmService osmService,
        IBookingMutationService mutationService,
        ILogger<BookingActionsController> logger)
    {
        _context = context;
        _osmService = osmService;
        _mutationService = mutationService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the line-items (sites and activities) for a booking.
    /// Returns 501 while the OSM item parsing seam is not yet wired up.
    /// </summary>
    [HttpGet("{id}/items")]
    public async Task<ActionResult<List<BookingItemDto>>> GetItems(int id)
    {
        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var items = await _osmService.GetBookingItemsAsync(booking.OsmBookingId);
            return Ok(items);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogInformation("OSM item retrieval not yet implemented for booking {Id}: {Message}", id, ex.Message);
            return StatusCode(501, new { message = "OSM item retrieval not yet implemented" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching items for booking {Id}", id);
            return StatusCode(502, new { message = "Error fetching items from OSM", detail = ex.Message });
        }
    }

    // ── Error mapping convention for mutation endpoints ───────────────────────
    // - 400 Bad Request: missing/invalid request parameters (checked before OSM calls)
    // - 404 Not Found: booking not in DB, or item not in booking's current item list
    // - 401 Unauthorized: InvalidOperationException whose message contains "OSM" (auth)
    // - 501 Not Implemented: NotImplementedException (OSM seam stubs not yet wired)
    // - 200 OK for all engine outcomes (completed/completed_with_warnings/rolled_back/failed):
    //   the caller reads result.Status — using 200 keeps the response contract simple and
    //   avoids conflating HTTP transport errors with application-level partial failures.

    /// <summary>
    /// Moves an activity item within the booking (changes start/end date or start/end time).
    /// Returns 501 while the OSM create/delete seam is not yet wired up.
    /// </summary>
    [HttpPost("{id}/actions/move-activity")]
    public async Task<ActionResult<BookingActionResult>> MoveActivity(int id, [FromBody] MoveActivityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
            return BadRequest(new { message = "ItemId is required" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var items = await _osmService.GetBookingItemsAsync(booking.OsmBookingId);
            var item = items.FirstOrDefault(i => i.ItemId == request.ItemId);
            if (item == null)
                return NotFound(new { message = $"Item '{request.ItemId}' not found in booking {id}" });

            var replacement = new ItemReplacement
            {
                Original = item,
                NewStartDate = request.NewStartDate,
                NewStartTime = request.NewStartTime,
                NewEndTime = request.NewEndTime
            };

            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, new[] { replacement });
            return Ok(result);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogInformation("OSM move-activity not yet implemented for booking {Id}: {Message}", id, ex.Message);
            return StatusCode(501, new { message = "OSM item mutation not yet implemented" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during move-activity for booking {Id}", id);
            return StatusCode(502, new { message = "Error executing move-activity", detail = ex.Message });
        }
    }

    /// <summary>
    /// Moves a site item to a different site within the booking.
    /// Returns 501 while the OSM create/delete seam is not yet wired up.
    /// </summary>
    [HttpPost("{id}/actions/change-site")]
    public async Task<ActionResult<BookingActionResult>> ChangeSite(int id, [FromBody] ChangeSiteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
            return BadRequest(new { message = "ItemId is required" });

        if (string.IsNullOrWhiteSpace(request.NewSiteId))
            return BadRequest(new { message = "NewSiteId is required" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var items = await _osmService.GetBookingItemsAsync(booking.OsmBookingId);
            var item = items.FirstOrDefault(i => i.ItemId == request.ItemId);
            if (item == null)
                return NotFound(new { message = $"Item '{request.ItemId}' not found in booking {id}" });

            var replacement = new ItemReplacement
            {
                Original = item,
                NewSiteId = request.NewSiteId
            };

            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, new[] { replacement });
            return Ok(result);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogInformation("OSM change-site not yet implemented for booking {Id}: {Message}", id, ex.Message);
            return StatusCode(501, new { message = "OSM item mutation not yet implemented" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during change-site for booking {Id}", id);
            return StatusCode(502, new { message = "Error executing change-site", detail = ex.Message });
        }
    }

    /// <summary>
    /// Shifts all items in the booking by the given number of days (positive = forward, negative = back).
    /// Each item with a StartDate gets a replacement with both StartDate and EndDate shifted.
    /// Items without a date are included unchanged.
    /// Returns 501 while the OSM create/delete seam is not yet wired up.
    /// </summary>
    [HttpPost("{id}/actions/move-dates")]
    public async Task<ActionResult<BookingActionResult>> MoveDates(int id, [FromBody] MoveDatesRequest request)
    {
        if (request.DayShift == 0)
            return BadRequest(new { message = "DayShift must be non-zero" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var items = await _osmService.GetBookingItemsAsync(booking.OsmBookingId);

            var replacements = items.Select(item => new ItemReplacement
            {
                Original = item,
                NewStartDate = item.StartDate.HasValue
                    ? item.StartDate.Value.AddDays(request.DayShift)
                    : null,
                NewEndDate = item.EndDate.HasValue
                    ? item.EndDate.Value.AddDays(request.DayShift)
                    : null
                // StartTime and EndTime are preserved (not overridden)
            }).ToList();

            var result = await _mutationService.ReplaceItemsAsync(booking.OsmBookingId, replacements);
            return Ok(result);
        }
        catch (NotImplementedException ex)
        {
            _logger.LogInformation("OSM move-dates not yet implemented for booking {Id}: {Message}", id, ex.Message);
            return StatusCode(501, new { message = "OSM item mutation not yet implemented" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during move-dates for booking {Id}", id);
            return StatusCode(502, new { message = "Error executing move-dates", detail = ex.Message });
        }
    }
}
