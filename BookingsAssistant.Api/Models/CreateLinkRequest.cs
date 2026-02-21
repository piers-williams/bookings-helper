using System.ComponentModel.DataAnnotations;

namespace BookingsAssistant.Api.Models;

public class CreateLinkRequest
{
    [Required]
    public int EmailMessageId { get; set; }

    [Required]
    public int OsmBookingId { get; set; }
}
