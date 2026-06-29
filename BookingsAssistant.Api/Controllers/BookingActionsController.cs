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
    private readonly ILogger<BookingActionsController> _logger;

    public BookingActionsController(
        ApplicationDbContext context,
        IOsmService osmService,
        ILogger<BookingActionsController> logger)
    {
        _context = context;
        _osmService = osmService;
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
}
