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
    public const string ArrivalPassed = "arrival_passed";
    public const string Scheduled = "scheduled";
    public const string Pending = "pending";

    /// <summary>
    /// A booking is sendable exactly when this returns <see cref="Pending"/>.
    ///
    /// Note: this deliberately does NOT consider whether we've pre-resolved the
    /// customer's email. Sending is a self-contained OSM workflow keyed off the
    /// booking id (<see cref="OsmService.SendBookingTemplateEmailAsync"/>), which
    /// resolves the recipient itself at send time. A booking is "pending" when
    /// it's eligible by timing/state; if the recipient can't be resolved the send
    /// fails loudly and is retried next run, rather than the booking being
    /// silently excluded here.
    /// </summary>
    public static string Evaluate(
        string status,
        DateTime startDate,
        DateTime? gateCodeSentAt,
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

        // Arrival is in the past but nothing was ever sent.
        if (startDate.Date < nowUtc.Date) return ArrivalPassed;

        // Confirmed and ready, but still outside the send window.
        if (startDate > nowUtc.AddDays(daysBefore)) return Scheduled;

        // Within the window, confirmed, not covered — sends on the next run.
        return Pending;
    }
}
