namespace PIGv4.Models;

public class ImportedSongViewModel
{
    public int SongId { get; set; }  // maps to PieceId
    public string FileName { get; set; } = "";
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int? BPM { get; set; }
    public int? Seconds { get; set; }
    public long? FileSize { get; set; }
    public uint? Track { get; set; }
    public string? Comment { get; set; }
}
