using System.ComponentModel.DataAnnotations;

namespace PIGv4.Models;

public class Config
{
    public int ConfigId { get; set; }
    
    [Required]
    public string AppDirectory { get; set; } = "";
    
    [Required]
    public string ConfigDirectory { get; set; } = "";
    
    [Required]
    public string MusicDirectory { get; set; } = "";
    
    [StringLength(50)]
    public string? LogFile { get; set; }
    
    [Required]
    public string PlayListDirectory { get; set; } = "";
    
    [StringLength(50)]
    public string? Creator { get; set; }
    
    [StringLength(50)]
    public string? Editor { get; set; }
    
    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime? Edited { get; set; }
    
    /// <summary>
    /// Target dB level for MP3 normalization (default 89.0 dB, same as MP3Gain default).
    /// </summary>
    public double TargetDb { get; set; } = 89.0;
}