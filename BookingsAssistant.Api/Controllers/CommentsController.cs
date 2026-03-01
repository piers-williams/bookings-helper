using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CommentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CommentDto>>> GetNew(
        [FromQuery] bool? newOnly = null,
        [FromQuery] int limit = 20,
        [FromQuery] DateTime? since = null)
    {
        var query = _context.OsmComments.AsQueryable();

        if (newOnly == true)
            query = query.Where(c => c.IsNew);

        if (since.HasValue)
            query = query.Where(c => c.CreatedDate >= since.Value);

        var clampedLimit = Math.Clamp(limit, 1, 100);

        // Fetch comments first, then join bookings in memory to avoid
        // EF Core InMemory provider limitations with sub-queries in projections
        var commentEntities = await query
            .OrderByDescending(c => c.CreatedDate)
            .Take(clampedLimit)
            .ToListAsync();

        var bookingIds = commentEntities.Select(c => c.OsmBookingId).Distinct().ToList();
        var bookings = await _context.OsmBookings
            .Where(b => bookingIds.Contains(b.OsmBookingId))
            .ToListAsync();

        var bookingMap = bookings.ToDictionary(b => b.OsmBookingId);

        var comments = commentEntities.Select(c =>
        {
            bookingMap.TryGetValue(c.OsmBookingId, out var booking);
            return new CommentDto
            {
                Id = c.Id,
                OsmBookingId = c.OsmBookingId,
                OsmCommentId = c.OsmCommentId,
                AuthorName = c.AuthorName,
                TextPreview = c.TextPreview ?? string.Empty,
                CreatedDate = c.CreatedDate,
                IsNew = c.IsNew,
                Booking = booking == null ? null : new BookingDto
                {
                    Id = booking.Id,
                    OsmBookingId = booking.OsmBookingId,
                    CustomerName = booking.CustomerName,
                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    Status = booking.Status
                }
            };
        }).ToList();

        return Ok(comments);
    }
}
