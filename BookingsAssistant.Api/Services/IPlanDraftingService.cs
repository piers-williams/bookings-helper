namespace BookingsAssistant.Api.Services;

/// <summary>
/// Drafts a structured action plan from a raw customer email (and, optionally,
/// booking context) by calling the configured LLM via <see cref="IOpenWebUiClient"/>.
/// </summary>
public interface IPlanDraftingService
{
    /// <summary>
    /// Builds a prompt from the email text (and booking context, if <paramref name="osmBookingId"/>
    /// is given), asks the LLM to draft a plan, and validates the response against the actions
    /// schema. Retries once on validation failure; never throws — a failure after the retry is
    /// reported via <see cref="PlanDraftResult.Success"/> being false.
    /// </summary>
    Task<PlanDraftResult> DraftPlanAsync(string sourceEmailText, string? osmBookingId);
}

/// <summary>
/// Outcome of a plan-drafting attempt. On success, <see cref="ActionsJson"/> holds the validated
/// `actions` JSON array (ready to store on <c>ProposedPlan.ActionsJson</c>). On failure,
/// <see cref="FailureReason"/> describes why the LLM's response(s) could not be used.
/// </summary>
public class PlanDraftResult
{
    public bool Success { get; init; }
    public string? ActionsJson { get; init; }
    public string? FailureReason { get; init; }

    /// <summary>
    /// Set on an otherwise-successful result when the automatic availability pre-check found a
    /// date-carrying action (e.g. addActivity) whose slot was still unavailable after the one
    /// retry drafting allows itself. Drafting still succeeds (<see cref="Success"/> is true) —
    /// the caller should copy this onto <c>ProposedPlan.DraftWarning</c> so a human reviewing
    /// the plan sees it before approving. Null in the common case (no conflict, or not checked).
    /// </summary>
    public string? Warning { get; init; }

    public static PlanDraftResult Ok(string actionsJson, string? warning = null) =>
        new() { Success = true, ActionsJson = actionsJson, Warning = warning };
    public static PlanDraftResult Fail(string reason) => new() { Success = false, FailureReason = reason };
}
