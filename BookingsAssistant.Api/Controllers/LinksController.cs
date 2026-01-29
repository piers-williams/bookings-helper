using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LinksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LinksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<LinkDto>> CreateLink([FromBody] CreateLinkRequest request)
    {
        // For Phase 1, userId is hardcoded - will be from auth in Phase 2
        var link = new ApplicationLink
        {
            EmailMessageId = request.EmailMessageId,
            OsmBookingId = request.OsmBookingId,
            CreatedByUserId = 1, // TODO: Get from authenticated user
            CreatedDate = DateTime.UtcNow
        };

        _context.ApplicationLinks.Add(link);
        await _context.SaveChangesAsync();

        var dto = new LinkDto
        {
            Id = link.Id,
            EmailMessageId = link.EmailMessageId,
            OsmBookingId = link.OsmBookingId,
            CreatedByUserId = link.CreatedByUserId,
            CreatedDate = link.CreatedDate
        };

        return CreatedAtAction(nameof(CreateLink), new { id = dto.Id }, dto);
    }

    [HttpGet]
    public async Task<ActionResult<List<LinkDto>>> GetLinks(
        [FromQuery] int? emailId = null,
        [FromQuery] int? bookingId = null)
    {
        var query = _context.ApplicationLinks.AsQueryable();

        if (emailId.HasValue)
            query = query.Where(l => l.EmailMessageId == emailId.Value);

        if (bookingId.HasValue)
            query = query.Where(l => l.OsmBookingId == bookingId.Value);

        var links = await query
            .Select(l => new LinkDto
            {
                Id = l.Id,
                EmailMessageId = l.EmailMessageId,
                OsmBookingId = l.OsmBookingId,
                CreatedByUserId = l.CreatedByUserId,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        return Ok(links);
    }
}
