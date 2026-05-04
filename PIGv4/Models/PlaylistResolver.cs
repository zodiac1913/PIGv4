using Microsoft.EntityFrameworkCore;

namespace PIGv4.Models;

/// <summary>
/// Manages two cache tables:
/// 1. PieceLookup — all song metadata without the Mp3 blob (~few MB vs 13GB Piece table)
/// 2. PlaylistSong — denormalized (ListId, PieceId) for instant playlist queries
/// All browse/filter queries go through PieceLookup. The Piece table is only touched for streaming.
/// </summary>
public static class PlaylistResolver
{
    // ── Playlist resolution ──────────────────────────────────────────

    public static async Task<List<int>> ResolvePieceIds(PigContext context, List<int> listIds)
    {
        if (listIds.Count == 0) return new List<int>();
        return await context.PlaylistSong
            .Where(ps => listIds.Contains(ps.ListId))
            .Select(ps => ps.PieceId)
            .Distinct()
            .ToListAsync();
    }

    public static async Task RebuildPlaylist(PigContext context, int listId)
    {
        await EnsureTables(context);
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM PlaylistSong WHERE ListId = {0}", listId);

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO PlaylistSong (ListId, PieceId)
            SELECT DISTINCT {0}, h.PieceId
            FROM ListFilter lf
            INNER JOIN PieceLookup h ON h.AudioHash = lf.AudioHash
            WHERE lf.ListId = {0} AND lf.HasTitle = 1", listId);

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT OR IGNORE INTO PlaylistSong (ListId, PieceId)
            SELECT DISTINCT {0}, h2.PieceId
            FROM ListFilter lf
            INNER JOIN PieceLookup h1 ON h1.AudioHash = lf.AudioHash
            INNER JOIN PieceLookup h2 ON h2.Artist = h1.Artist
            WHERE lf.ListId = {0} AND lf.HasArtist = 1
            AND h1.Artist IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM PlaylistSong ps
                WHERE ps.ListId = {0} AND ps.PieceId = h2.PieceId
            )", listId);
    }

    public static async Task RebuildPlaylists(PigContext context, IEnumerable<int> listIds)
    {
        foreach (var id in listIds)
            await RebuildPlaylist(context, id);
    }

    // ── Startup ──────────────────────────────────────────────────────

    public static async Task RebuildAll(PigContext context)
    {
        await EnsureTables(context);
        await RefreshLookup(context);

        using var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PlaylistSong";
        await context.Database.OpenConnectionAsync();
        var existing = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        if (existing > 0) return;

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO PlaylistSong (ListId, PieceId)
            SELECT DISTINCT lf.ListId, h.PieceId
            FROM ListFilter lf
            INNER JOIN PieceLookup h ON h.AudioHash = lf.AudioHash
            WHERE lf.HasTitle = 1");

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT OR IGNORE INTO PlaylistSong (ListId, PieceId)
            SELECT DISTINCT lf.ListId, h2.PieceId
            FROM ListFilter lf
            INNER JOIN PieceLookup h1 ON h1.AudioHash = lf.AudioHash
            INNER JOIN PieceLookup h2 ON h2.Artist = h1.Artist
            WHERE lf.HasArtist = 1
            AND h1.Artist IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM PlaylistSong ps
                WHERE ps.ListId = lf.ListId AND ps.PieceId = h2.PieceId
            )");
    }

    // ── PieceLookup management ───────────────────────────────────────

    /// <summary>
    /// Populate PieceLookup from Piece if empty. One-time cost on first boot.
    /// </summary>
    public static async Task RefreshLookup(PigContext context)
    {
        using var cmd = context.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PieceLookup";
        await context.Database.OpenConnectionAsync();
        var existing = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        if (existing > 0) return;

        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO PieceLookup (PieceId, AudioHash, Artist, Title, Album, Genre, Year, BPM, Seconds, SourceFolder, FileName, FileSize, IsNew, AlbumArtUrl, AlbumArtChecked)
            SELECT PieceId, AudioHash, Artist, Title, Album, Genre, Year, BPM, Seconds, SourceFolder, FileName, FileSize, IsNew, AlbumArtUrl, AlbumArtChecked
            FROM Piece");
    }

    /// <summary>Add or update a single song in the lookup. Call after import or tag edit.</summary>
    public static async Task UpdateLookup(PigContext context, Piece piece)
    {
        await EnsureTables(context);
        await context.Database.ExecuteSqlRawAsync(@"
            INSERT OR REPLACE INTO PieceLookup 
            (PieceId, AudioHash, Artist, Title, Album, Genre, Year, BPM, Seconds, SourceFolder, FileName, FileSize, IsNew, AlbumArtUrl, AlbumArtChecked)
            VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14})",
            piece.PieceId, piece.AudioHash,
            piece.Artist ?? (object)DBNull.Value,
            piece.Title ?? (object)DBNull.Value,
            piece.Album ?? (object)DBNull.Value,
            piece.Genre ?? (object)DBNull.Value,
            piece.Year.HasValue ? piece.Year.Value : DBNull.Value,
            piece.BPM.HasValue ? piece.BPM.Value : DBNull.Value,
            piece.Seconds.HasValue ? piece.Seconds.Value : DBNull.Value,
            piece.SourceFolder ?? (object)DBNull.Value,
            piece.FileName,
            piece.FileSize.HasValue ? piece.FileSize.Value : DBNull.Value,
            piece.IsNew ? 1 : 0,
            piece.AlbumArtUrl ?? (object)DBNull.Value,
            piece.AlbumArtChecked ? 1 : 0);
    }

    /// <summary>Remove a song from the lookup. Call after delete.</summary>
    public static async Task RemoveLookup(PigContext context, int pieceId)
    {
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM PieceLookup WHERE PieceId = {0}", pieceId);
    }

    // ── Table setup ──────────────────────────────────────────────────

    private static async Task EnsureTables(PigContext context)
    {
        await context.Database.OpenConnectionAsync();

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS PieceLookup (
                PieceId INTEGER PRIMARY KEY,
                AudioHash TEXT NOT NULL,
                Artist TEXT,
                Title TEXT,
                Album TEXT,
                Genre TEXT,
                Year INTEGER,
                BPM INTEGER,
                Seconds INTEGER,
                SourceFolder TEXT,
                FileName TEXT,
                FileSize INTEGER,
                IsNew INTEGER DEFAULT 1,
                AlbumArtUrl TEXT,
                AlbumArtChecked INTEGER DEFAULT 0
            )");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PieceLookup_AudioHash ON PieceLookup(AudioHash)");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PieceLookup_Artist ON PieceLookup(Artist)");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PieceLookup_Genre ON PieceLookup(Genre)");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PieceLookup_SourceFolder ON PieceLookup(SourceFolder)");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS PlaylistSong (
                PlaylistSongId INTEGER PRIMARY KEY AUTOINCREMENT,
                ListId INTEGER NOT NULL,
                PieceId INTEGER NOT NULL
            )");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PlaylistSong_ListId ON PlaylistSong(ListId)");
        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_PlaylistSong_ListId_PieceId ON PlaylistSong(ListId, PieceId)");
    }
}
