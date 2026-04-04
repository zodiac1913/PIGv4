using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

[Table("ListFilter")]
public class ListFilter
{
    public int ListFilterId { get; set; }
    
    /// <summary>
    /// References ListModel.ListId — simple int join.
    /// </summary>
    public int ListId { get; set; }
    
    /// <summary>
    /// References ListModel.UniqueId — legacy, kept for compatibility.
    /// </summary>
    public Guid ListUniqueId { get; set; }
    
    /// <summary>
    /// References Piece.AudioHash — no FK, loose join.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string AudioHash { get; set; } = "";
    
    public bool? HasArtist { get; set; }
    public bool? HasTitle { get; set; }
    public bool? HasGenre { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Creator { get; set; } = "";
    
    public DateTime Created { get; set; } = DateTime.Now;
    
    [StringLength(50)]
    public string? Editor { get; set; }
    
    public DateTime? Edited { get; set; }
}
