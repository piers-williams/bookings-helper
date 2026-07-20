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
    private readonly IOsmService _osmService;
    private readonly IBookingItemActionService _itemActionService;
    private readonly ILogger<BookingActionsController> _logger;

    public BookingActionsController(
        ApplicationDbContext context,
        IOsmService osmService,
        IBookingItemActionService itemActionService,
        ILogger<BookingActionsController> logger)
    {
        _context = context;
        _osmService = osmService;
        _itemActionService = itemActionService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the line-items (sites and activities) for a booking.
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

    /// <summary>
    /// Lists the bookable sites/pitches the booking's site items could be moved to (for change-site).
    /// </summary>
    [HttpGet("{id}/available-sites")]
    public async Task<ActionResult<List<AvailableSiteDto>>> GetAvailableSites(int id)
    {
        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var sites = await _osmService.GetAvailableSitesAsync(booking.OsmBookingId);
            return Ok(sites);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available sites for booking {Id}", id);
            return StatusCode(502, new { message = "Error fetching sites from OSM", detail = ex.Message });
        }
    }

    // ── Error mapping convention for mutation endpoints ───────────────────────
    // - 400 Bad Request: missing/invalid request parameters (checked before OSM calls)
    // - 404 Not Found: booking not in DB, or item not in booking's current item list
    // - 401 Unauthorized: InvalidOperationException whose message contains "OSM" (auth)
    // - 502 Bad Gateway: other OSM failures (e.g. no available slot, OSM rejected the create)
    // - 200 OK for all engine outcomes (completed/completed_with_warnings/rolled_back/failed):
    //   the caller reads result.Status — using 200 keeps the response contract simple and
    //   avoids conflating HTTP transport errors with application-level partial failures.

    /// <summary>
    /// Moves an activity item within the booking (changes start/end date or start/end time).
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
            var result = await _itemActionService.MoveActivityAsync(booking.OsmBookingId, request);
            return Ok(result);
        }
        catch (BookingItemNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
            var result = await _itemActionService.ChangeSiteAsync(booking.OsmBookingId, request);
            return Ok(result);
        }
        catch (BookingItemNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
            var result = await _itemActionService.MoveDatesAsync(booking.OsmBookingId, request);
            return Ok(result);
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
