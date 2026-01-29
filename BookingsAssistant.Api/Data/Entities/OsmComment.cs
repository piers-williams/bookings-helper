using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("OsmComments")]
public class OsmComment
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string OsmBookingId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string OsmCommentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string AuthorName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TextPreview { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public bool IsNew { get; set; }

    public DateTime? LastFetched { get; set; }

    // Navigation property
    [ForeignKey(nameof(OsmBookingId))]
    public OsmBooking? Booking { get; set; }
}
