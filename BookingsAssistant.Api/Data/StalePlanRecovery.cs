using BookingsAssistant.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookingsAssistant.Api.Data;

/// <summary>
/// One-time startup sweep (see Program.cs) that recovers <see cref="ProposedPlan"/> rows left
/// stuck in <see cref="PlanStatus.Processing"/> by a crash or unhandled error between
/// PlansController's atomic "claim" step (AwaitingApproval -> Processing, see
/// <see cref="Services.PlanTransitionLock"/>) and the terminal status write that normally
/// follows immediately after (Executed/Failed/Rejected).
///
/// Processing is documented as a transient, in-request-only marker: no request can still
/// legitimately be "in progress" across a process restart, since the in-process
/// PlanTransitionLock semaphore and any in-flight request are gone along with the old process.
/// So on startup, any plan still in Processing is stale by definition and is reset to Failed —
/// a safe terminal state with no re-execution path, visible in the Triage UI for a human to
/// investigate, consistent with how every other Failed plan behaves.
///
/// SourceEmailText is purged at the same time, consistent with the rule that Failed plans carry
/// no PII (see PII inventory in CLAUDE.md) — the same purge Approve/Reject/Create already do
/// whenever a plan reaches a terminal state.
/// </summary>
public static class StalePlanRecovery
{
    public static async Task<int> RecoverStaleProcessingPlansAsync(ApplicationDbContext context, ILogger logger)
    {
        var stalePlans = await context.ProposedPlans
            .Where(p => p.Status == PlanStatus.Processing)
            .ToListAsync();

        if (stalePlans.Count == 0)
            return 0;

        foreach (var plan in stalePlans)
        {
            plan.Status = PlanStatus.Failed;
            plan.SourceEmailText = null;
        }

        await context.SaveChangesAsync();

        logger.LogWarning(
            "Startup stale-plan recovery: reset {Count} plan(s) stuck in Processing to Failed",
            stalePlans.Count);

        return stalePlans.Count;
    }
}
