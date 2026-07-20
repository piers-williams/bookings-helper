using System.Text.Json;
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
    private readonly IPlanExecutionService _planExecutionService;
    private readonly PlanTransitionLock _transitionLock;

    public PlansController(
        ApplicationDbContext context,
        IPlanDraftingService planDraftingService,
        IPlanExecutionService planExecutionService,
        PlanTransitionLock transitionLock)
    {
        _context = context;
        _planDraftingService = planDraftingService;
        _planExecutionService = planExecutionService;
        _transitionLock = transitionLock;
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
                ExecutionResultJson = p.ExecutionResultJson,
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
            // Drafting failed permanently (both LLM attempts invalid), and Failed plans have
            // no Approve/Reject path to purge this later — purge now so raw customer email
            // text never lingers past a failed drafting attempt (see PII inventory in CLAUDE.md).
            plan.SourceEmailText = null;
        }
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = plan.Id }, ToDto(plan));
    }

    /// <summary>
    /// Approves an AwaitingApproval plan and executes its actions against OSM, in order,
    /// stopping at the first failure. Sets Status to Executed (all actions succeeded/no-op)
    /// or Failed (an action failed), records per-action results in ExecutionResultJson, and
    /// purges SourceEmailText since it's no longer needed and carries PII.
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ProposedPlanDto>> Approve(int id)
    {
        var (plan, error) = await TryClaimAwaitingApprovalAsync(id, "approved");
        if (error != null)
            return error;

        var outcome = await _planExecutionService.ExecuteAsync(plan!);

        plan!.Status = outcome.Success ? PlanStatus.Executed : PlanStatus.Failed;
        plan.ExecutionResultJson = JsonSerializer.Serialize(outcome.Results);
        plan.SourceEmailText = null;
        await _context.SaveChangesAsync();

        return Ok(ToDto(plan));
    }

    /// <summary>
    /// Rejects an AwaitingApproval plan. No OSM calls are made; the plan is marked Rejected
    /// and SourceEmailText is purged since it's no longer needed and carries PII.
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult<ProposedPlanDto>> Reject(int id)
    {
        var (plan, error) = await TryClaimAwaitingApprovalAsync(id, "rejected");
        if (error != null)
            return error;

        plan!.Status = PlanStatus.Rejected;
        plan.SourceEmailText = null;
        await _context.SaveChangesAsync();

        return Ok(ToDto(plan));
    }

    /// <summary>
    /// Atomically claims a plan for a status transition (Approve/Reject), preventing the
    /// TOCTOU race where two near-simultaneous requests both read Status == AwaitingApproval,
    /// both pass the check, and both proceed to execute/reject the same plan.
    ///
    /// Under <see cref="PlanTransitionLock"/>, reads the plan, checks it's still
    /// AwaitingApproval, and — if so — flips it to Processing (a transient "claimed" marker,
    /// see <see cref="PlanStatus"/>) and saves, all before releasing the lock. A concurrent
    /// caller blocked on the lock will, once it acquires it, see Status == Processing (or
    /// whatever terminal status the winner already reached) and is turned away with 409,
    /// never proceeding to execute/reject. The lock is released before the caller goes on to
    /// do the (potentially slow) OSM execution, so unrelated plans' claim steps aren't blocked.
    /// See <see cref="PlanTransitionLock"/> for why a lock rather than a DB-level atomic
    /// conditional update (e.g. ExecuteUpdateAsync) is used here.
    /// </summary>
    private async Task<(ProposedPlan? Plan, ActionResult? Error)> TryClaimAwaitingApprovalAsync(int id, string actionDescription)
    {
        using var _ = await _transitionLock.AcquireAsync();

        var plan = await _context.ProposedPlans.FindAsync(id);
        if (plan == null)
            return (null, NotFound());

        if (plan.Status != PlanStatus.AwaitingApproval)
            return (null, Conflict(new { message = $"Plan {id} cannot be {actionDescription} (status: {plan.Status})" }));

        plan.Status = PlanStatus.Processing;
        await _context.SaveChangesAsync();

        return (plan, null);
    }

    private static ProposedPlanDto ToDto(ProposedPlan plan) => new()
    {
        Id = plan.Id,
        Status = plan.Status.ToString(),
        SourceEmailText = plan.SourceEmailText,
        OsmBookingId = plan.OsmBookingId,
        ActionsJson = plan.ActionsJson,
        ExecutionResultJson = plan.ExecutionResultJson,
        CreatedAt = plan.CreatedAt
    };
}
