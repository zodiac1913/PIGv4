using Microsoft.EntityFrameworkCore;

namespace PIGv4.Models;

/// <summary>
/// Resolves the full song list for a Gen Playlist.
/// HasTitle = that specific song is in the playlist.
/// HasArtist = ALL songs by that artist are in the playlist.
/// </summary>
public static class PlaylistResolver
{
    public static async Task<List<int>> ResolvePieceIds(PigContext context, int listId)
    {
        var filters = await context.ListFilter
            .Where(lf => lf.ListId == listId)
            .ToListAsync();

        // Songs explicitly added by title
        var titleHashes = filters.Where(f => f.HasTitle == true).Select(f => f.AudioHash).ToList();

        // Artists added — get all their songs
        var artistHashes = filters.Where(f => f.HasArtist == true).Select(f => f.AudioHash).ToList();
        var artistNames = await context.PieceInfo
            .Where(p => artistHashes.Contains(p.AudioHash) && p.Artist != null)
            .Select(p => p.Artist!)
            .Distinct()
            .ToListAsync();

        var artistSongIds = await context.PieceInfo
            .Where(p => p.Artist != null && artistNames.Contains(p.Artist))
            .Select(p => p.PieceId)
            .ToListAsync();

        // Songs by title hash
        var titleSongIds = await context.PieceInfo
            .Where(p => titleHashes.Contains(p.AudioHash))
            .Select(p => p.PieceId)
            .ToListAsync();

        // Combine and deduplicate
        var allIds = new HashSet<int>(titleSongIds);
        foreach (var id in artistSongIds) allIds.Add(id);

        return allIds.ToList();
    }

    public static async Task<List<string>> ResolveAudioHashes(PigContext context, int listId)
    {
        var filters = await context.ListFilter
            .Where(lf => lf.ListId == listId)
            .ToListAsync();

        var titleHashes = filters.Where(f => f.HasTitle == true).Select(f => f.AudioHash).ToHashSet();

        var artistHashes = filters.Where(f => f.HasArtist == true).Select(f => f.AudioHash).ToList();
        var artistNames = await context.PieceInfo
            .Where(p => artistHashes.Contains(p.AudioHash) && p.Artist != null)
            .Select(p => p.Artist!)
            .Distinct()
            .ToListAsync();

        var artistSongHashes = await context.PieceInfo
            .Where(p => p.Artist != null && artistNames.Contains(p.Artist))
            .Select(p => p.AudioHash)
            .ToListAsync();

        foreach (var h in artistSongHashes) titleHashes.Add(h);
        return titleHashes.ToList();
    }
}
