using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

/// <summary>
/// Lightweight mirror of Piece without the Mp3 blob.
/// All browse/filter/player queries go through this table (~few MB) instead of Piece (~13GB).
/// Kept in sync by PlaylistResolver.UpdateLookup on import, edit, and delete.
/// </summary>
[Table("PieceLookup")]
public class PieceLookup
{
    public int PieceId { get; set; }
    public string AudioHash { get; set; } = "";
    public string? Artist { get; set; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int? BPM { get; set; }
    public int? Seconds { get; set; }
    public string? SourceFolder { get; set; }
    public string FileName { get; set; } = "";
    public long? FileSize { get; set; }
    public bool IsNew { get; set; }
    public string? AlbumArtUrl { get; set; }
    public bool AlbumArtChecked { get; set; }
}
