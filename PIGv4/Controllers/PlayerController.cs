using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class PlayerController : Controller
{
    private readonly PigContext _context;

    public PlayerController(PigContext context) => _context = context;

    /// <summary>Stream MP3 audio for a given PieceId.</summary>
    [HttpGet]
    public async Task<IActionResult> Stream(int id)
    {
        var piece = await _context.Piece
            .Where(p => p.PieceId == id)
            .Select(p => new { p.Mp3 })
            .FirstOrDefaultAsync();

        if (piece?.Mp3 == null) return NotFound();

        return File(piece.Mp3, "audio/mpeg");
    }

    /// <summary>Browse songs with optional filters. Returns lightweight JSON (no blob).</summary>
    [HttpGet]
    public async Task<IActionResult> Browse(
        [FromQuery] List<string>? genres,
        [FromQuery] List<string>? artists,
        [FromQuery] List<string>? folders,
        [FromQuery] List<int>? listIds,
        string? search,
        int page = 1, int pageSize = 50)
    {
        var query = _context.PieceInfo.AsQueryable();

        // If Gen Playlists are selected, resolve full song lists (title + artist expansion)
        if (listIds != null && listIds.Count > 0)
        {
            var allHashes = new HashSet<string>();
            foreach (var lid in listIds)
            {
                var hashes = await PlaylistResolver.ResolveAudioHashes(_context, lid);
                foreach (var h in hashes) allHashes.Add(h);
            }
            var hashList = allHashes.ToList();
            query = query.Where(p => hashList.Contains(p.AudioHash));
        }

        if (genres != null && genres.Count > 0)
            query = query.Where(p => p.Genre != null && genres.Contains(p.Genre));
        if (artists != null && artists.Count > 0)
            query = query.Where(p => p.Artist != null && artists.Contains(p.Artist));
        if (folders != null && folders.Count > 0)
            query = query.Where(p => p.SourceFolder != null && folders.Contains(p.SourceFolder));

        // Christmas blackout: Jan 15 – Thanksgiving (4th Thursday of November)
        if (!IsChristmasSeason())
            query = query.Where(p => p.SourceFolder != "Christmas");
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                (p.Title != null && p.Title.Contains(search)) ||
                (p.Artist != null && p.Artist.Contains(search)) ||
                (p.Album != null && p.Album.Contains(search)));

        var total = await query.CountAsync();
        var songs = await query
            .OrderBy(p => p.Artist).ThenBy(p => p.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.PieceId, p.Title, p.Artist, p.Album, p.Genre,
                p.Year, p.BPM, p.Seconds, p.SourceFolder
            })
            .ToListAsync();

        return Json(new { total, page, pageSize, songs });
    }

    /// <summary>Get a single song by PieceId (for picked songs).</summary>
    [HttpGet]
    public async Task<IActionResult> BrowseById(int id)
    {
        var song = await _context.PieceInfo
            .Where(p => p.PieceId == id)
            .Select(p => new
            {
                p.PieceId, p.Title, p.Artist, p.Album, p.Genre,
                p.Year, p.BPM, p.Seconds, p.SourceFolder, p.AudioHash
            })
            .FirstOrDefaultAsync();

        if (song == null) return Json(new { song = (object?)null });

        var playlistNames = await _context.ListFilter
            .Where(lf => lf.AudioHash == song.AudioHash)
            .Join(_context.List, lf => lf.ListId, l => l.ListId, (lf, l) => l.Title)
            .Distinct().OrderBy(t => t).ToListAsync();

        return Json(new { song, playlists = playlistNames });
    }

    /// <summary>Get album art embedded in the MP3 file.</summary>
    [HttpGet]
    public async Task<IActionResult> AlbumArt(int id)
    {
        var piece = await _context.Piece
            .Where(p => p.PieceId == id)
            .Select(p => new { p.Mp3, p.FileName })
            .FirstOrDefaultAsync();

        if (piece?.Mp3 == null) return NotFound();

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");
            await System.IO.File.WriteAllBytesAsync(tempPath, piece.Mp3);
            using var tagFile = TagLib.File.Create(tempPath);
            System.IO.File.Delete(tempPath);

            if (tagFile.Tag.Pictures.Length > 0)
            {
                var pic = tagFile.Tag.Pictures[0];
                return File(pic.Data.Data, pic.MimeType);
            }
        }
        catch { }

        return NotFound();
    }

    /// <summary>Get distinct filter values. Pass type=folders, genres, or artists.</summary>
    [HttpGet]
    public async Task<IActionResult> Filters(string? type)
    {
        if (type == "folders")
        {
            var fQuery = _context.PieceInfo.Where(p => p.SourceFolder != null);
            if (!IsChristmasSeason())
                fQuery = fQuery.Where(p => p.SourceFolder != "Christmas");
            var folders = await fQuery.Select(p => p.SourceFolder!).Distinct().OrderBy(f => f).ToListAsync();
            return Json(folders);
        }
        if (type == "genres")
        {
            var genres = await _context.PieceInfo.Where(p => p.Genre != null)
                .Select(p => p.Genre!).Distinct().OrderBy(g => g).ToListAsync();
            return Json(genres);
        }
        if (type == "artists")
        {
            var artists = await _context.PieceInfo.Where(p => p.Artist != null)
                .Select(p => p.Artist!).Distinct().OrderBy(a => a).ToListAsync();
            return Json(artists);
        }
        if (type == "playlists")
        {
            var plQuery = _context.List.AsQueryable();
            if (!IsChristmasSeason())
                plQuery = plQuery.Where(l => !l.Title.Contains("Christmas"));
            var playlists = await plQuery
                .OrderBy(l => l.Title)
                .Select(l => new { l.ListId, l.Title })
                .ToListAsync();
            return Json(playlists);
        }

        // Legacy: return all at once
        var allGenres = await _context.PieceInfo.Where(p => p.Genre != null)
            .Select(p => p.Genre!).Distinct().OrderBy(g => g).ToListAsync();
        var allArtists = await _context.PieceInfo.Where(p => p.Artist != null)
            .Select(p => p.Artist!).Distinct().OrderBy(a => a).ToListAsync();
        var allFolders = await _context.PieceInfo.Where(p => p.SourceFolder != null)
            .Select(p => p.SourceFolder!).Distinct().OrderBy(f => f).ToListAsync();

        return Json(new { genres = allGenres, artists = allArtists, folders = allFolders });
    }

    /// <summary>
    /// Christmas season: Thanksgiving (4th Thursday of Nov) through Jan 15, inclusive.
    /// Off-season: Jan 16 through day before Thanksgiving.
    /// </summary>
    private static bool IsChristmasSeason()
    {
        var today = DateTime.Today;
        var year = today.Year;

        // Jan 1–15 inclusive = still Christmas season from previous year
        if (today.Month == 1 && today.Day <= 15) return true;

        // Find Thanksgiving: 4th Thursday of November this year
        var nov1 = new DateTime(year, 11, 1);
        var firstThursday = nov1.AddDays((DayOfWeek.Thursday - nov1.DayOfWeek + 7) % 7);
        var thanksgiving = firstThursday.AddDays(21); // 4th Thursday

        // Thanksgiving through Dec 31 inclusive
        if (today >= thanksgiving) return true;

        return false;
    }
}
