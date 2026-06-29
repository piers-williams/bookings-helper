namespace BookingsAssistant.Api.Models;

/// <summary>
/// A bookable site/pitch the user can move a booked item to (for the change-site action).
/// Sourced from the OSM item-type catalogue.
/// </summary>
public class AvailableSiteDto
{
    /// <summary>OSM item-type id (campsite_item_id), used as newSiteId in a change-site request.</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
