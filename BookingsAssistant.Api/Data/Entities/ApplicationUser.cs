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

    [Required]
    [MaxLength(255)]
    public string Office365Email { get; set; } = string.Empty;

    public string? Office365AccessToken { get; set; }
    public string? Office365RefreshToken { get; set; }
    public DateTime? Office365TokenExpiry { get; set; }

    [MaxLength(255)]
    public string? OsmUsername { get; set; }
    public string? OsmApiToken { get; set; }
    public DateTime? OsmTokenExpiry { get; set; }

    public DateTime? LastSync { get; set; }
}
