using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

/// <summary>
/// API token for mobile app authentication.
/// Generated on login, sent as "Authorization: Bearer {Token}" on every request.
/// </summary>
[Table("ApiToken")]
public class ApiToken
{
    public int ApiTokenId { get; set; }

    /// <summary>The user this token belongs to.</summary>
    public int AppUserId { get; set; }

    /// <summary>Random 64-char hex token.</summary>
    [Required]
    [StringLength(128)]
    public string Token { get; set; } = "";

    /// <summary>Friendly name (e.g. "Pixel 8", "iPhone").</summary>
    [StringLength(100)]
    public string? DeviceName { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;

    /// <summary>Last time this token was used.</summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>Set to false to revoke without deleting.</summary>
    public bool IsActive { get; set; } = true;
}
