using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class ArtistsController : Controller
{
    private readonly PigContext _context;

    public ArtistsController(PigContext context) => _context = context;

    public IActionResult Index() => View();

    /// <summary>Get paginated list of distinct artists with song counts.</summary>
    [HttpGet]
    public async Task<IActionResult> Browse(string? search, string? startsWith, int page = 1, int pageSize = 50)
    {
        var query = _context.PieceInfo
            .Where(p => p.Artist != null && p.Artist != "");

        if (!string.IsNullOrWhiteSpace(startsWith))
            query = query.Where(p => p.Artist!.ToLower().StartsWith(startsWith.ToLower()));
        else if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Artist!.ToLower().Contains(search.ToLower()));

        var artists = await query
            .GroupBy(p => p.Artist)
            .Select(g => new { Artist = g.Key, SongCount = g.Count() })
            .OrderBy(a => a.Artist)
            .ToListAsync();

        var total = artists.Count;
        var paged = artists.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Json(new { total, page, pageSize, artists = paged });
    }

    /// <summary>Get detail for an artist: songs, genres, playlists, and possible duplicates.</summary>
    [HttpGet]
    public async Task<IActionResult> Detail(string name)
    {
        if (string.IsNullOrEmpty(name)) return BadRequest();

        // Songs by this artist
        var songs = await _context.PieceInfo
            .Where(p => p.Artist == name)
            .OrderBy(p => p.Title)
            .Select(p => new { p.PieceId, p.Title, p.Album, p.Genre, p.Year, p.BPM, p.Seconds })
            .ToListAsync();

        // Genres this artist appears in
        var genres = await _context.PieceInfo
            .Where(p => p.Artist == name && p.Genre != null)
            .Select(p => p.Genre!).Distinct().OrderBy(g => g).ToListAsync();

        // Gen Playlists this artist is in (via ListFilter HasArtist)
        var audioHashes = await _context.PieceInfo
            .Where(p => p.Artist == name)
            .Select(p => p.AudioHash).Distinct().ToListAsync();

        var playlistIds = await _context.ListFilter
            .Where(lf => audioHashes.Contains(lf.AudioHash) && (lf.HasArtist == true || lf.HasTitle == true))
            .Select(lf => lf.ListUniqueId).Distinct().ToListAsync();

        var playlists = await _context.List
            .Where(l => playlistIds.Contains(l.UniqueId))
            .OrderBy(l => l.Title)
            .Select(l => l.Title).ToListAsync();

        // Find possible duplicates — similar artist names
        var allArtists = await _context.PieceInfo
            .Where(p => p.Artist != null && p.Artist != "")
            .Select(p => p.Artist!).Distinct().ToListAsync();

        var normalized = NormalizeName(name);
        var duplicates = allArtists
            .Where(a => a != name && NormalizeName(a) == normalized)
            .ToList();

        return Json(new { name, songs, genres, playlists, duplicates });
    }

    private static string NormalizeName(string name)
    {
        return name.ToLower()
            .Replace(",", "").Replace(".", "").Replace(" ", "")
            .Replace("&", "and")
            .Replace("'", "").Replace("\u2019", "")  // Apostrophe + right single quote
            .Replace("\u2010", "-")  // Unicode hyphen
            .Replace("\u2011", "-")  // Non-breaking hyphen
            .Replace("\u2012", "-")  // Figure dash
            .Replace("\u2013", "-")  // En dash
            .Replace("\u2014", "-")  // Em dash
            .Replace("\u2015", "-"); // Horizontal bar
    }

    /// <summary>Merge duplicate artist names into one canonical name.</summary>
    [HttpPost]
    public async Task<IActionResult> Merge([FromBody] MergeRequest req)
    {
        if (string.IsNullOrEmpty(req.CanonicalName) || req.OldNames == null || req.OldNames.Count == 0)
            return BadRequest();

        foreach (var oldName in req.OldNames)
        {
            var pieces = await _context.Piece
                .Where(p => p.Artist == oldName)
                .ToListAsync();

            foreach (var p in pieces)
            {
                p.Artist = req.CanonicalName;
                p.Editor = "ArtistMerge";
                p.Edited = DateTime.Now;

                // Update the MP3 tag too
                if (p.Mp3 != null)
                {
                    try
                    {
                        var tempPath = Path.Combine(Path.GetTempPath(), p.FileName);
                        await System.IO.File.WriteAllBytesAsync(tempPath, p.Mp3);
                        using var tagFile = TagLib.File.Create(tempPath);
                        tagFile.Tag.Performers = new[] { req.CanonicalName };
                        tagFile.Save();
                        p.Mp3 = await System.IO.File.ReadAllBytesAsync(tempPath);
                        System.IO.File.Delete(tempPath);
                    }
                    catch { }
                }
            }
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true, merged = req.OldNames.Count });
    }
}

public class MergeRequest
{
    public string CanonicalName { get; set; } = "";
    public List<string> OldNames { get; set; } = new();
}
