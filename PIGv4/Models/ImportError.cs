using System.ComponentModel.DataAnnotations;

namespace PIGv4.Models;

public class ImportError
{
    public long ImportErrorId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Source { get; set; } = "";
    
    [Required]
    public string Error { get; set; } = "";
    
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime? Edited { get; set; }
    
    [StringLength(50)]
    public string? Creator { get; set; }
    
    [StringLength(50)]
    public string? Editor { get; set; }
}