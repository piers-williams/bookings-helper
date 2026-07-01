namespace BookingsAssistant.Api.Models;

public class ChangeSiteRequest
{
    public string ItemId { get; set; } = string.Empty;
    public string NewSiteId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the target site, as already shown in the frontend's available-sites
    /// dropdown. Used to build a readable audit comment; falls back to NewSiteId if omitted.
    /// </summary>
    public string? NewSiteName { get; set; }

    /// <summary>Optional free-text note appended to the auto-generated audit comment.</summary>
    public string? Note { get; set; }
}
