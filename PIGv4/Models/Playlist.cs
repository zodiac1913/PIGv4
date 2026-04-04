using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

[Table("List")]
public class ListModel
{
    public int ListId { get; set; }
    
    /// <summary>
    /// Stable unique identifier. Survives renames.
    /// </summary>
    public Guid UniqueId { get; set; } = Guid.NewGuid();
    
    [Required]
    [StringLength(50)]
    public string Title { get; set; } = "";
    
    public int Minimum { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Creator { get; set; } = "";
    
    public DateTime Created { get; set; } = DateTime.Now;
    
    [StringLength(50)]
    public string? Editor { get; set; }
    
    public DateTime? Edited { get; set; }
}
