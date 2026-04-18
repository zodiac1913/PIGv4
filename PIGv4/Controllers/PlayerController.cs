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
        // Build separate queries for each filter type, then UNION them (OR logic)
        var queries = new List<IQueryable<PieceInfo>>();

        if (listIds != null && listIds.Count > 0)
        {
            var allHashes = await PlaylistResolver.ResolveAudioHashes(_context, listIds);
            queries.Add(_context.PieceInfo.Where(p => allHashes.Contains(p.AudioHash)));
        }

        if (genres != null && genres.Count > 0)
            queries.Add(_context.PieceInfo.Where(p => p.Genre != null && genres.Contains(p.Genre)));
        if (artists != null && artists.Count > 0)
            queries.Add(_context.PieceInfo.Where(p => p.Artist != null && artists.Contains(p.Artist)));
        if (folders != null && folders.Count > 0)
            queries.Add(_context.PieceInfo.Where(p => p.SourceFolder != null && folders.Contains(p.SourceFolder)));

        IQueryable<PieceInfo> query;
        if (queries.Count == 0)
        {
            // No filters — return empty
            return Json(new { total = 0, page, pageSize, songs = new List<object>() });
        }
        else if (queries.Count == 1)
        {
            query = queries[0];
        }
        else
        {
            // Union all queries
            query = queries[0];
            for (int i = 1; i < queries.Count; i++)
                query = query.Union(queries[i]);
        }

        // Christmas blackout
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

    /// <summary>Get album art: embedded in MP3, or return cached URL for external art.</summary>
    [HttpGet]
    public async Task<IActionResult> AlbumArt(int id)
    {
        var piece = await _context.Piece
            .Where(p => p.PieceId == id)
            .Select(p => new { p.PieceId, p.Mp3, p.AlbumArtUrl, p.AlbumArtChecked, p.Artist, p.Album })
            .FirstOrDefaultAsync();

        if (piece == null) return NotFound();

        // 1. If we have a cached URL, return it as JSON
        if (!string.IsNullOrEmpty(piece.AlbumArtUrl))
            return Json(new { url = piece.AlbumArtUrl });

        // 2. Already checked and nothing found
        if (piece.AlbumArtChecked)
            return NotFound();

        // 3. Try embedded art in MP3
        if (piece.Mp3 != null)
        {
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
        }

        // 4. Fire background lookup for URL (Cover Art Archive → Wikipedia)
        _ = Task.Run(async () => await FetchAndCacheArtUrl(piece.PieceId, piece.Artist, piece.Album));

        // Mark as checked
        var p2 = await _context.Piece.FindAsync(piece.PieceId);
        if (p2 != null)
        {
            p2.AlbumArtChecked = true;
            await _context.SaveChangesAsync();
        }

        return NotFound();
    }

    private static readonly HttpClient _artClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "PIGv4/1.0 (playlist-intelligent-generator)" } }
    };

    private async Task FetchAndCacheArtUrl(int pieceId, string? artist, string? album)
    {
        if (string.IsNullOrEmpty(artist)) return;
        string? artUrl = null;

        // Step 1: Try Cover Art Archive
        if (!string.IsNullOrEmpty(album))
        {
            try
            {
                var searchUrl = $"https://musicbrainz.org/ws/2/release/?query=artist:{Uri.EscapeDataString(artist)}+release:{Uri.EscapeDataString(album)}&fmt=json&limit=1";
                var searchJson = await _artClient.GetStringAsync(searchUrl);
                using var doc = System.Text.Json.JsonDocument.Parse(searchJson);
                var releases = doc.RootElement.GetProperty("releases");
                if (releases.GetArrayLength() > 0)
                {
                    var releaseId = releases[0].GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(releaseId))
                        artUrl = $"https://coverartarchive.org/release/{releaseId}/front-250";
                }
            }
            catch { }
        }

        // Step 2: Try Wikipedia artist image
        if (string.IsNullOrEmpty(artUrl))
        {
            try
            {
                await Task.Delay(500);
                var wikiUrl = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(artist.Replace(" ", "_"))}";
                var html = await _artClient.GetStringAsync(wikiUrl);

                var marker = "infobox-image";
                var idx = html.IndexOf(marker);
                if (idx > 0)
                {
                    var imgStart = html.IndexOf("<img", idx);
                    if (imgStart > 0 && imgStart < idx + 2000)
                    {
                        var srcStart = html.IndexOf("src=\"", imgStart);
                        if (srcStart > 0)
                        {
                            srcStart += 5;
                            var srcEnd = html.IndexOf("\"", srcStart);
                            var imgSrc = html.Substring(srcStart, srcEnd - srcStart);
                            if (imgSrc.StartsWith("//")) imgSrc = "https:" + imgSrc;
                            artUrl = imgSrc;
                        }
                    }
                }
            }
            catch { }
        }

        // Save URL if found
        if (!string.IsNullOrEmpty(artUrl))
        {
            try
            {
                var connStr = _context.Database.GetConnectionString();
                var opts = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PigContext>()
                    .UseSqlite(connStr).Options;
                using var db = new PigContext(opts);
                var entity = await db.Piece.FindAsync(pieceId);
                if (entity != null)
                {
                    entity.AlbumArtUrl = artUrl;
                    entity.AlbumArtChecked = true;
                    await db.SaveChangesAsync();
                }
            }
            catch { }
        }
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
