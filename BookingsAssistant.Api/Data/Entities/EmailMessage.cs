using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("EmailMessages")]
public class EmailMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string SenderEmail { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? SenderName { get; set; }

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public DateTime ReceivedDate { get; set; }

    public bool IsRead { get; set; }

    [MaxLength(50)]
    public string? ExtractedBookingRef { get; set; }

    public DateTime? LastFetched { get; set; }

    // Navigation properties
    public ICollection<ApplicationLink> Links { get; set; } = new List<ApplicationLink>();
}
