namespace BookingsAssistant.Api.Models;

/// <summary>
/// Changes the headcount (number of people) on an existing booking item. Like
/// MoveActivityRequest/ChangeSiteRequest, this clones the original item (via
/// IBookingMutationService.ReplaceItemsAsync) with NumberPeople overridden, then deletes
/// the original — it does not mutate the item in place.
/// </summary>
public class ChangeNumbersRequest
{
    public string ItemId { get; set; } = string.Empty;
    public int? NewNumberPeople { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
