using BookingsAssistant.Api.Data;
using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;
using BookingsAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingsAssistant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlansController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPlanDraftingService _planDraftingService;

    public PlansController(ApplicationDbContext context, IPlanDraftingService planDraftingService)
    {
        _context = context;
        _planDraftingService = planDraftingService;
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

    [HttpGet("{id}")]
    public async Task<ActionResult<ProposedPlanDto>> GetById(int id)
    {
        var plan = await _context.ProposedPlans.FindAsync(id);
        if (plan == null)
            return NotFound();

        return Ok(ToDto(plan));
    }

    /// <summary>
    /// Drafts a new action plan from a customer email via the LLM. A ProposedPlan row is
    /// persisted immediately (before drafting runs) so there's always a record, even if
    /// drafting fails. On success the row is updated with the validated actions JSON,
    /// staying AwaitingApproval; on failure (both LLM attempts invalid) it becomes Failed.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ProposedPlanDto>> Create([FromBody] CreatePlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceEmailText))
            return BadRequest(new { message = "sourceEmailText is required" });

        var plan = new ProposedPlan
        {
            Status = PlanStatus.AwaitingApproval,
            SourceEmailText = request.SourceEmailText,
            OsmBookingId = request.OsmBookingId,
            CreatedAt = DateTime.UtcNow
        };
        _context.ProposedPlans.Add(plan);
        await _context.SaveChangesAsync();

        var draftResult = await _planDraftingService.DraftPlanAsync(request.SourceEmailText, request.OsmBookingId);
        if (draftResult.Success)
        {
            plan.ActionsJson = draftResult.ActionsJson;
        }
        else
        {
            plan.Status = PlanStatus.Failed;
        }
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = plan.Id }, ToDto(plan));
    }

    private static ProposedPlanDto ToDto(ProposedPlan plan) => new()
    {
        Id = plan.Id,
        Status = plan.Status.ToString(),
        SourceEmailText = plan.SourceEmailText,
        OsmBookingId = plan.OsmBookingId,
        ActionsJson = plan.ActionsJson,
        CreatedAt = plan.CreatedAt
    };
}
