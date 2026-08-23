namespace BookingsAssistant.Api.Models;

/// <summary>
/// String constants for PlanActionExecutionResult.Status.
/// </summary>
public static class PlanActionExecutionStatus
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string NotAttempted = "not_attempted";
}

/// <summary>
/// The outcome of executing a single action from a ProposedPlan's ActionsJson. One of these
/// is recorded per action, in order, whether or not the action was actually attempted
/// (actions after the first failure are recorded as "not_attempted").
/// </summary>
public class PlanActionExecutionResult
{
    /// <summary>The action's "type" field (e.g. "postComment", "moveDates").</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>One of: "succeeded", "failed", "not_attempted". See <see cref="PlanActionExecutionStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Present when Status is "failed" — a human-readable explanation.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Present for actions that produce a result beyond success/failure (currently only
    /// "checkAvailability" — e.g. "Available" or "Not available: no slot covers 12-14 Aug").
    /// Unlike <see cref="Reason"/>, this is populated on a "succeeded" outcome too.
    /// </summary>
    public string? Detail { get; set; }
}
