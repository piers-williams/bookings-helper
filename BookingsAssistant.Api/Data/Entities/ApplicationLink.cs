using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("ApplicationLinks")]
public class ApplicationLink
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EmailMessageId { get; set; }

    [Required]
    public int OsmBookingId { get; set; }

    // Nullable - null means auto-linked
    public int? CreatedByUserId { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    // Navigation properties
    [ForeignKey(nameof(EmailMessageId))]
    public EmailMessage EmailMessage { get; set; } = null!;

    [ForeignKey(nameof(OsmBookingId))]
    public OsmBooking OsmBooking { get; set; } = null!;

    [ForeignKey(nameof(CreatedByUserId))]
    public ApplicationUser? CreatedByUser { get; set; }
}
