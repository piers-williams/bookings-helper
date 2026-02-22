using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookingsAssistant.Api.Data.Entities;

[Table("ApplicationUsers")]
public class ApplicationUser
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? OsmUsername { get; set; }
    public string? OsmAccessToken { get; set; }
    public string? OsmRefreshToken { get; set; }
    public DateTime? OsmTokenExpiry { get; set; }

    public DateTime? LastSync { get; set; }
}
