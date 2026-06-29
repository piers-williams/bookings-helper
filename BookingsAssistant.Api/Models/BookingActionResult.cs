namespace BookingsAssistant.Api.Models;

/// <summary>
/// String constants for BookingActionResult.Status.
/// Using plain string constants to match the project's style (OsmBooking.Status is a plain string).
/// </summary>
public static class BookingActionStatus
{
    public const string Completed = "completed";
    public const string CompletedWithWarnings = "completed_with_warnings";
    public const string RolledBack = "rolled_back";
    public const string Failed = "failed";
}

/// <summary>
/// The result of a booking mutation operation (e.g. item replacement).
/// </summary>
public class BookingActionResult
{
    /// <summary>Ids of items successfully created during this operation.</summary>
    public List<string> Created { get; set; } = new();

    /// <summary>Ids of items successfully deleted during this operation.</summary>
    public List<string> Deleted { get; set; } = new();

    /// <summary>
    /// One of: "completed", "completed_with_warnings", "rolled_back", "failed".
    /// See <see cref="BookingActionStatus"/> for the constants.
    /// </summary>
    public string Status { get; set; } = BookingActionStatus.Failed;

    /// <summary>Human-readable explanation of the outcome.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The booking's items after the operation completes (best-effort; may reflect
    /// the pre-rollback state if GetBookingItemsAsync itself fails).
    /// </summary>
    public List<BookingItemDto> Items { get; set; } = new();
}
