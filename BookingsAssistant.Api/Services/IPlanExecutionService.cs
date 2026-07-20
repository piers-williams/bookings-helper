using BookingsAssistant.Api.Data.Entities;
using BookingsAssistant.Api.Models;

namespace BookingsAssistant.Api.Services;

/// <summary>
/// Executes the actions of an approved <see cref="ProposedPlan"/> against OSM. This is the
/// only place in the system allowed to mutate OSM state on the LLM's behalf, and only ever
/// runs after a human has approved the plan (see PlansController.Approve).
/// </summary>
public interface IPlanExecutionService
{
    /// <summary>
    /// Executes each action in <paramref name="plan"/>.ActionsJson, in order, stopping at the
    /// first failure. Does not mutate <paramref name="plan"/> or touch the database — the
    /// caller (PlansController) is responsible for applying the resulting status and
    /// persisting it.
    /// </summary>
    Task<PlanExecutionOutcome> ExecuteAsync(ProposedPlan plan);
}

/// <summary>
/// Result of executing a plan's actions: whether every action succeeded (or was a no-op),
/// and the per-action results (one per action, in order — including "not_attempted" entries
/// for actions after the first failure).
/// </summary>
public class PlanExecutionOutcome
{
    public bool Success { get; init; }
    public List<PlanActionExecutionResult> Results { get; init; } = new();
}
