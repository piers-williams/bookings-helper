namespace BookingsAssistant.Api.Services;

/// <summary>
/// Thrown by <see cref="IBookingItemActionService"/> when a requested ItemId isn't present
/// in the booking's current item list. Callers map this to whatever "not found" response
/// makes sense for their context (e.g. BookingActionsController maps it to a 404; plan
/// execution maps it to a failed-with-reason action result).
/// </summary>
public class BookingItemNotFoundException : Exception
{
    public BookingItemNotFoundException(string itemId, string osmBookingId)
        : base($"Item '{itemId}' not found in booking {osmBookingId}")
    {
    }
}
