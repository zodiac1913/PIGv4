using System.ComponentModel.DataAnnotations;

namespace PIGv4.Models;

public class MP3Genre
{
    public int GenreId { get; set; }
    
    [StringLength(50)]
    public string? GenreName { get; set; }
    
    public DateTime? Created { get; set; }
    public DateTime? Edited { get; set; }
    
    [StringLength(50)]
    public string? Creator { get; set; }
    
    [StringLength(50)]
    public string? Editor { get; set; }
}