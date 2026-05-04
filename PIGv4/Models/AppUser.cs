using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

[Table("AppUser")]
public class AppUser
{
    public int AppUserId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Username { get; set; } = "";
    
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = "";
    
    [StringLength(50)]
    public string? DisplayName { get; set; }
    
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "User"; // Admin, User
    
    public bool IsApproved { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime Created { get; set; } = DateTime.Now;
    
    public DateTime? LastLogin { get; set; }
}
