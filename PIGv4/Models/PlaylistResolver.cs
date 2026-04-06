using Microsoft.EntityFrameworkCore;

namespace PIGv4.Models;

/// <summary>
/// Resolves the full song list for Gen Playlists.
/// HasTitle = that specific song is in the playlist.
/// HasArtist = ALL songs by that artist are in the playlist.
/// Uses raw SQL for performance — single query per playlist.
/// </summary>
public static class PlaylistResolver
{
    /// <summary>
    /// Resolve all audio hashes for one or more playlists in a single query.
    /// </summary>
    public static async Task<List<string>> ResolveAudioHashes(PigContext context, int listId)
    {
        return await ResolveAudioHashes(context, new List<int> { listId });
    }

    public static async Task<List<string>> ResolveAudioHashes(PigContext context, List<int> listIds)
    {
        if (listIds.Count == 0) return new List<string>();

        var idList = string.Join(",", listIds);

        // Single SQL: get direct title hashes + all songs by flagged artists
        var sql = $@"
            SELECT DISTINCT p.AudioHash FROM PieceInfo p
            WHERE p.AudioHash IN (
                SELECT lf.AudioHash FROM ListFilter lf 
                WHERE lf.ListId IN ({idList}) AND lf.HasTitle = 1
            )
            UNION
            SELECT DISTINCT p2.AudioHash FROM PieceInfo p2
            WHERE p2.Artist IN (
                SELECT DISTINCT p3.Artist FROM PieceInfo p3
                INNER JOIN ListFilter lf2 ON p3.AudioHash = lf2.AudioHash
                WHERE lf2.ListId IN ({idList}) AND lf2.HasArtist = 1
                AND p3.Artist IS NOT NULL
            )";

        var hashes = new List<string>();
        using var cmd = context.Database.GetDbConnection().CreateCommand();
        await context.Database.OpenConnectionAsync();
        cmd.CommandText = sql;
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            hashes.Add(reader.GetString(0));
        }
        return hashes;
    }
}
