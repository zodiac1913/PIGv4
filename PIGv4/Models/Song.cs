using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

[Table("Piece")]
public class Piece
{
    public int PieceId { get; set; }
    
    /// <summary>
    /// SHA256 hash of audio-only data (excludes ID3 tags).
    /// Stable across tag edits, renames, re-imports.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string AudioHash { get; set; } = "";
    
    [StringLength(255)]
    public string? Artist { get; set; }
    
    [StringLength(255)]
    public string? Title { get; set; }
    
    [StringLength(255)]
    public string? Genre { get; set; }
    
    [StringLength(255)]
    public string? Album { get; set; }
    
    public int? Year { get; set; }
    public int? Seconds { get; set; }
    public int? BPM { get; set; }
    
    [Required]
    [StringLength(4000)]
    public string FileName { get; set; } = "";
    
    [StringLength(500)]
    public string? SourceFolder { get; set; }
    
    public long? FileSize { get; set; }
    
    public byte[]? Mp3 { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Creator { get; set; } = "";
    
    public DateTime Created { get; set; } = DateTime.Now;
    
    [StringLength(50)]
    public string? Editor { get; set; }
    
    public DateTime? Edited { get; set; }
    
    public bool IsNew { get; set; } = true;
}


/// <summary>
/// Read-only view of Piece without the Mp3 blob. For fast browse queries.
/// </summary>
public class PieceInfo
{
    public int PieceId { get; set; }
    public string AudioHash { get; set; } = "";
    public string? Artist { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public string? Album { get; set; }
    public int? Year { get; set; }
    public int? Seconds { get; set; }
    public int? BPM { get; set; }
    public string FileName { get; set; } = "";
    public long? FileSize { get; set; }
    public string? SourceFolder { get; set; }
    public string Creator { get; set; } = "";
    public DateTime Created { get; set; }
    public string? Editor { get; set; }
    public DateTime? Edited { get; set; }
    public bool IsNew { get; set; }
}
