using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlansController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PlansController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProposedPlanDto>>> GetAll([FromQuery] string? status = null)
    {
        var query = _context.ProposedPlans.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<PlanStatus>(status, ignoreCase: true, out var parsedStatus))
                return BadRequest(new { message = $"Invalid status '{status}'. Valid values: {string.Join(", ", Enum.GetNames<PlanStatus>())}" });

            query = query.Where(p => p.Status == parsedStatus);
        }

        var plans = await query
            .Select(p => new ProposedPlanDto
            {
                Id = p.Id,
                Status = p.Status.ToString(),
                SourceEmailText = p.SourceEmailText,
                OsmBookingId = p.OsmBookingId,
                ActionsJson = p.ActionsJson,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(plans);
    }
}
