using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("OsmBookings")]
public class OsmBooking
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string OsmBookingId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? CustomerEmailHash { get; set; }

    [MaxLength(64)]
    public string? CustomerNameHash { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // Provisional, Confirmed, Cancelled

    public DateTime? LastFetched { get; set; }

    // Navigation properties
    public ICollection<OsmComment> Comments { get; set; } = new List<OsmComment>();
    public ICollection<ApplicationLink> Links { get; set; } = new List<ApplicationLink>();
}
