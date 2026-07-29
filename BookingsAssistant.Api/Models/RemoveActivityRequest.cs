namespace BookingsAssistant.Api.Models;

/// <summary>
/// Removes (hard-deletes) an existing booking item — activity or site. Unlike
/// MoveActivityRequest/ChangeSiteRequest, there is no replacement item: the original is
/// resolved by ItemId and deleted directly via IOsmService.DeleteBookingItemAsync.
/// </summary>
public class RemoveActivityRequest
{
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
