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

    /// <summary>
    /// Lists the bookable activities that could be added to the booking (for add-activity).
    /// </summary>
    [HttpGet("{id}/available-activities")]
    public async Task<ActionResult<List<AvailableSiteDto>>> GetAvailableActivities(int id)
    {
        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var activities = await _osmService.GetAvailableActivitiesAsync(booking.OsmBookingId);
            return Ok(activities);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available activities for booking {Id}", id);
            return StatusCode(502, new { message = "Error fetching activities from OSM", detail = ex.Message });
        }
    }

    /// <summary>
    /// Read-only check of whether a site/activity item-type has an available slot for the
    /// given date range. Unlike the mutation endpoints below, "not available" is a normal 200
    /// result (AvailabilityResult.Available = false) — it is not treated as an error.
    /// </summary>
    [HttpGet("{id}/availability")]
    public async Task<ActionResult<AvailabilityResult>> CheckAvailability(
        int id, [FromQuery] string? activityId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(activityId))
            return BadRequest(new { message = "activityId is required" });

        if (startDate is null)
            return BadRequest(new { message = "startDate is required" });

        if (endDate is null)
            return BadRequest(new { message = "endDate is required" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var result = await _osmService.CheckAvailabilityAsync(booking.OsmBookingId, activityId, startDate.Value, endDate.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability for booking {Id}", id);
            return StatusCode(502, new { message = "Error checking availability with OSM", detail = ex.Message });
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

    /// <summary>
    /// Adds a brand-new activity item to the booking (no existing item to clone — the create
    /// spec is built entirely from the request).
    /// </summary>
    [HttpPost("{id}/actions/add-activity")]
    public async Task<ActionResult<BookingActionResult>> AddActivity(int id, [FromBody] AddActivityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ActivityId))
            return BadRequest(new { message = "ActivityId is required" });

        if (request.StartDate is null)
            return BadRequest(new { message = "StartDate is required" });

        if (request.EndDate is null)
            return BadRequest(new { message = "EndDate is required" });

        if (request.NumberPeople is null)
            return BadRequest(new { message = "NumberPeople is required" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var result = await _itemActionService.AddActivityAsync(booking.OsmBookingId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OSM"))
        {
            return Unauthorized(new { message = "OSM authentication required", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during add-activity for booking {Id}", id);
            return StatusCode(502, new { message = "Error executing add-activity", detail = ex.Message });
        }
    }

    /// <summary>
    /// Removes (hard-deletes) an existing item — activity or site — from the booking. No
    /// replacement is created; this is a straight delete of an existing item.
    /// </summary>
    [HttpPost("{id}/actions/remove-activity")]
    public async Task<ActionResult<BookingActionResult>> RemoveActivity(int id, [FromBody] RemoveActivityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
            return BadRequest(new { message = "ItemId is required" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var result = await _itemActionService.RemoveActivityAsync(booking.OsmBookingId, request);
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
            _logger.LogError(ex, "Error during remove-activity for booking {Id}", id);
            return StatusCode(502, new { message = "Error executing remove-activity", detail = ex.Message });
        }
    }

    /// <summary>
    /// Changes the headcount (number of people) on an existing item. Uses the same
    /// clone-then-delete-original engine as move-activity/change-site.
    /// </summary>
    [HttpPost("{id}/actions/change-numbers")]
    public async Task<ActionResult<BookingActionResult>> ChangeNumbers(int id, [FromBody] ChangeNumbersRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
            return BadRequest(new { message = "ItemId is required" });

        // Upper bound intentionally omitted: OSM enforces its own per-site/per-activity
        // capacity limits, which surface as a 502 (OSM rejected the create) rather than a
        // client-side guess we can't validate without visibility into pitch capacity.
        if (request.NewNumberPeople is null or <= 0)
            return BadRequest(new { message = "NewNumberPeople is required and must be greater than zero" });

        var booking = await _context.OsmBookings.FindAsync(id);
        if (booking == null)
            return NotFound();

        try
        {
            var result = await _itemActionService.ChangeNumbersAsync(booking.OsmBookingId, request);
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
            _logger.LogError(ex, "Error during change-numbers for booking {Id}", id);
            return StatusCode(502, new { message = "Error executing change-numbers", detail = ex.Message });
        }
    }
}
