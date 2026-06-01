namespace BookingsAssistant.Api.Services;

/// <summary>
/// Single source of truth for a booking's gate-code status. Shared by the
/// display API (<see cref="Controllers.BookingsController"/>) and the sender
/// (<see cref="GateCodeService"/>) so the dashboard always reflects the real
/// reason a code has — or has not — been sent.
///
/// The non-terminal reasons mirror, in order, the exclusion conditions in
/// <see cref="GateCodeService.ProcessPendingBookingsAsync"/>. Keep them in sync.
/// </summary>
public static class GateCodeStatusEvaluator
{
    public const string Sent = "sent";
    public const string NotRequired = "not_required";
    public const string AwaitingConfirmation = "awaiting_confirmation";
    public const string AwaitingEmailSync = "awaiting_email_sync";
    public const string NoEmail = "no_email";
    public const string ArrivalPassed = "arrival_passed";
    public const string Scheduled = "scheduled";
    public const string Pending = "pending";

    /// <summary>
    /// A booking is sendable exactly when this returns <see cref="Pending"/>.
    /// </summary>
    public static string Evaluate(
        string status,
        DateTime startDate,
        DateTime? gateCodeSentAt,
        string? customerEmailHash,
        bool coveredByDuty,
        DateTime nowUtc,
        int daysBefore)
    {
        // Already done.
        if (gateCodeSentAt != null) return Sent;

        // Someone is on site to let them in, so no code is needed at all —
        // this takes priority over the "outstanding" reasons below.
        if (coveredByDuty) return NotRequired;

        // From here the code is genuinely outstanding; explain why it hasn't
        // gone out yet, matching the sender's exclusion conditions in order.
        if (!string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return AwaitingConfirmation;

        // Backfill hasn't populated the customer email hash yet.
        if (customerEmailHash == null) return AwaitingEmailSync;

        // OSM has no usable email for this booking ("no-email" sentinel).
        if (customerEmailHash == "no-email") return NoEmail;

        // Arrival is in the past but nothing was ever sent.
        if (startDate.Date < nowUtc.Date) return ArrivalPassed;

        // Confirmed and ready, but still outside the send window.
        if (startDate > nowUtc.AddDays(daysBefore)) return Scheduled;

        // Within the window, has an email, not covered — sends on the next run.
        return Pending;
    }
}
