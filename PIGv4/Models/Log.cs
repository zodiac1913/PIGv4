using System.ComponentModel.DataAnnotations;

namespace PIGv4.Models;

public class Log
{
    public long LogIdentifier { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = "";
    
    [Required]
    public string Message { get; set; } = "";
    
    public string? Exception { get; set; }
    
    public int Severity { get; set; }
    
    [StringLength(100)]
    public string? ClassName { get; set; }
    
    [StringLength(100)]
    public string? MethodName { get; set; }
    
    [StringLength(50)]
    public string? Creator { get; set; }
    
    public DateTime Created { get; set; } = DateTime.Now;
    
    [StringLength(50)]
    public string? Editor { get; set; }
    
    public DateTime? Edited { get; set; }
}