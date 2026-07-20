using BookingsAssistant.Api.Data;
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
    public async Task<ActionResult<List<ProposedPlanDto>>> GetAll()
    {
        var plans = await _context.ProposedPlans
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
