namespace BookingsAssistant.Api.Models;

/// <summary>
/// Describes a single item to be replaced within a booking mutation operation.
/// Carries the original item (to be deleted after successful creation) plus
/// the field overrides to apply to the clone.
/// </summary>
public class ItemReplacement
{
    /// <summary>The original booking item that will be deleted after the clone is created.</summary>
    public BookingItemDto Original { get; set; } = new();

    /// <summary>If set, overrides the SiteId on the cloned item.</summary>
    public string? NewSiteId { get; set; }

    /// <summary>If set, overrides the StartDate on the cloned item.</summary>
    public DateTime? NewStartDate { get; set; }

    /// <summary>If set, overrides the EndDate on the cloned item.</summary>
    public DateTime? NewEndDate { get; set; }

    /// <summary>If set, overrides the StartTime on the cloned item.</summary>
    public string? NewStartTime { get; set; }

    /// <summary>If set, overrides the EndTime on the cloned item.</summary>
    public string? NewEndTime { get; set; }
}
