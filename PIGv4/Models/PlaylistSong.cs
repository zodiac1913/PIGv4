using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIGv4.Models;

/// <summary>
/// Denormalized cache: which PieceIds belong to which ListId.
/// Rebuilt from ListFilter + Piece whenever playlist assignments change.
/// Eliminates the need to join through AudioHash and the blob-heavy Piece table at query time.
/// </summary>
[Table("PlaylistSong")]
public class PlaylistSong
{
    public int PlaylistSongId { get; set; }
    public int ListId { get; set; }
    public int PieceId { get; set; }
}
