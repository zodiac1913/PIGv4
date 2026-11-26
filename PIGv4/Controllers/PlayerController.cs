using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class PlayerController : Controller
{
    private readonly PigContext _context;

    public PlayerController(PigContext context) => _context = context;

    // ── Samsung TV (Tizen) support ──────────────────────────────────
    // The Tizen media player fetches audio without the login cookie, so it gets a signed,
    // song-specific ticket in the URL instead. Only the TV page asks for one; every other
    // browser keeps using the cookie exactly as before.
    private static readonly TimeSpan StreamTicketLifetime = TimeSpan.FromHours(6);
    private ITimeLimitedDataProtector StreamTicketProtector() =>
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("PIGv4.StreamTicket").ToTimeLimitedDataProtector();
    /// <summary>Signed stream URL for one song (Samsung TV only). Valid 6 hours, this song only.</summary>
    [HttpGet]
    public IActionResult StreamUrl(int id)
    {
        var ticket = StreamTicketProtector().Protect($"{id}|{User.Identity?.Name}", StreamTicketLifetime);
        return Json(new { url = $"/Player/Stream?id={id}&st={Uri.EscapeDataString(ticket)}" });
    }
    private async Task<bool> IsValidStreamTicket(int id, string? st)
    {
        if (string.IsNullOrEmpty(st)) return false;
        string payload;
        try { payload = StreamTicketProtector().Unprotect(st, out _); }
        catch { return false; } // tampered, expired, or unknown key
        var parts = payload.Split('|', 2);
        if (parts.Length != 2 || parts[0] != id.ToString()) return false;
        var username = parts[1];
        return await _context.AppUser.AnyAsync(u => u.Username == username && u.IsActive && u.IsApproved);
    }
    /// <summary>Stream MP3 audio for a given PieceId. Login cookie, or a Samsung TV stream ticket.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Stream(int id, string? st)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            if (!await IsValidStreamTicket(id, st))
                return Redirect("/Account/Login?ReturnUrl=" + Uri.EscapeDataString(Request.Path + Request.QueryString));
        }
        var piece = await _context.Piece
            .Where(p => p.PieceId == id)
            .Select(p => new { p.Mp3 })
            .FirstOrDefaultAsync();

        if (piece?.Mp3 == null)
        {
            Console.WriteLine($"Stream: no MP3 data for PieceId {id}");
            return NotFound();
        }

        // Range support lets browsers seek and resume after a dropped connection (Safari/iOS require it)
        return File(piece.Mp3, "audio/mpeg", enableRangeProcessing: true);
    }

    /// <summary>Browse songs with optional filters. Returns lightweight JSON (no blob).</summary>
    [HttpGet]
    public async Task<IActionResult> Browse(
        [FromQuery] List<string>? genres,
        [FromQuery] List<string>? artists,
        [FromQuery] List<string>? folders,
        [FromQuery] List<int>? listIds,
        string? search,
        bool all = false,
        int page = 1, int pageSize = 50)
    {
        // Build separate queries for each filter type, then UNION them (OR logic)
        var queries = new List<IQueryable<PieceLookup>>();

        if (listIds != null && listIds.Count > 0)
        {
            var pieceIds = await PlaylistResolver.ResolvePieceIds(_context, listIds);
            queries.Add(_context.PieceLookup.Where(p => pieceIds.Contains(p.PieceId)));
        }

        if (genres != null && genres.Count > 0)
            queries.Add(_context.PieceLookup.Where(p => p.Genre != null && genres.Contains(p.Genre)));
        if (artists != null && artists.Count > 0)
            queries.Add(_context.PieceLookup.Where(p => p.Artist != null && artists.Contains(p.Artist)));
        if (folders != null && folders.Count > 0)
            queries.Add(_context.PieceLookup.Where(p => p.SourceFolder != null && folders.Contains(p.SourceFolder)));

        IQueryable<PieceLookup> query;
        if (queries.Count == 0)
        {
            if (all)
            {
                // No filters but all=true — return all songs
                query = _context.PieceLookup;
            }
            else
            {
                return Json(new { total = 0, page, pageSize, songs = new List<object>() });
            }
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

    /// <summary>Get a single random song (fast — uses PieceHashLookup, no blob scan).</summary>
    [HttpGet]
    public async Task<IActionResult> RandomSong()
    {
        var count = await _context.PieceLookup.CountAsync();
        if (count == 0) return Json(new { song = (object?)null });

        var skip = new Random().Next(count);
        var song = await _context.PieceLookup
            .OrderBy(p => p.PieceId)
            .Skip(skip).Take(1)
            .Select(p => new
            {
                p.PieceId, p.Title, p.Artist, p.Album, p.Genre,
                p.Year, p.BPM, p.Seconds, p.SourceFolder
            })
            .FirstOrDefaultAsync();

        return Json(new { song });
    }

    /// <summary>Get a single song by PieceId (for picked songs).</summary>
    [HttpGet]
    public async Task<IActionResult> BrowseById(int id)
    {
        var song = await _context.PieceLookup
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

    /// <summary>
    /// Get album art: embedded in MP3, or the cached online URL.
    /// When there is none, a background lookup runs on every play until art is found.
    /// </summary>
    [HttpGet]
    /// <param name="poll">True when the page is only checking on a running lookup; does not start a new one.</param>
    public async Task<IActionResult> AlbumArt(int id, bool poll = false)
    {
        var piece = await _context.Piece
            .Where(p => p.PieceId == id)
            .Select(p => new { p.PieceId, p.Mp3, p.AlbumArtUrl, p.Artist, p.Album, p.Year })
            .FirstOrDefaultAsync();
        if (piece == null) return NotFound();
        // 1. Cached online art (only ever saved for songs without embedded art)
        if (!string.IsNullOrEmpty(piece.AlbumArtUrl))
            return Json(new { url = piece.AlbumArtUrl,
                              isArtistPhoto = piece.AlbumArtUrl.EndsWith(AlbumArtLookup.ArtistPhotoMarker) });
        // 2. Embedded art in the MP3
        if (piece.Mp3 != null)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");
            try
            {
                await System.IO.File.WriteAllBytesAsync(tempPath, piece.Mp3);
                using var tagFile = TagLib.File.Create(tempPath);
                if (tagFile.Tag.Pictures.Length > 0)
                {
                    var pic = tagFile.Tag.Pictures[0];
                    return File(pic.Data.Data, pic.MimeType);
                }
            }
            catch { }
            finally { try { System.IO.File.Delete(tempPath); } catch { } }
        }
        // 3. Nothing yet: look it up in the background (retried on every play)
        if (!poll) StartArtLookup(piece.PieceId, piece.Artist, piece.Album, piece.Year);
        return NotFound(new { searching = AlbumArtLookup.IsRunning(piece.PieceId) });
    }
    /// <summary>Reject the current online album art and look for a different one.</summary>
    [HttpPost]
    public async Task<IActionResult> RejectAlbumArt(int id)
    {
        var piece = await _context.Piece.FindAsync(id);
        if (piece == null) return NotFound();
        if (!string.IsNullOrEmpty(piece.AlbumArtUrl))
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT OR IGNORE INTO AlbumArtRejected (PieceId, Url) VALUES ({id}, {piece.AlbumArtUrl})");
            piece.AlbumArtUrl = null;
            piece.AlbumArtChecked = false;
            await _context.SaveChangesAsync();
            await PlaylistResolver.UpdateLookup(_context, piece);
        }
        StartArtLookup(piece.PieceId, piece.Artist, piece.Album, piece.Year);
        return Ok(new { searching = AlbumArtLookup.IsRunning(piece.PieceId) });
    }
    private void StartArtLookup(int pieceId, string? artist, string? album, int? year)
    {
        if (!AlbumArtLookup.TryBegin(pieceId)) return;
        var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PigContext>();
                var rejected = (await db.Database
                    .SqlQuery<string>($"SELECT Url AS Value FROM AlbumArtRejected WHERE PieceId = {pieceId}")
                    .ToListAsync()).ToHashSet();
                var artUrl = await AlbumArtLookup.FindAsync(artist, album, year, rejected);
                if (string.IsNullOrEmpty(artUrl)) return;
                var entity = await db.Piece.FindAsync(pieceId);
                if (entity == null) return;
                entity.AlbumArtUrl = artUrl;
                entity.AlbumArtChecked = true;
                await db.SaveChangesAsync();
                await PlaylistResolver.UpdateLookup(db, entity);
            }
            catch (Exception ex) { Console.WriteLine($"Album art lookup failed for {pieceId}: {ex.Message}"); }
            finally { AlbumArtLookup.End(pieceId); }
        });
    }
    /// <summary>Get distinct filter values. Pass type=folders, genres, or artists.</summary>
    [HttpGet]
    public async Task<IActionResult> Filters(string? type)
    {
        if (type == "folders")
        {
            var fQuery = _context.PieceLookup.Where(p => p.SourceFolder != null);
            if (!IsChristmasSeason())
                fQuery = fQuery.Where(p => p.SourceFolder != "Christmas");
            var folders = await fQuery.Select(p => p.SourceFolder!).Distinct().OrderBy(f => f).ToListAsync();
            return Json(folders);
        }
        if (type == "genres")
        {
            var genres = await _context.PieceLookup.Where(p => p.Genre != null)
                .Select(p => p.Genre!).Distinct().OrderBy(g => g).ToListAsync();
            return Json(genres);
        }
        if (type == "artists")
        {
            var artists = await _context.PieceLookup.Where(p => p.Artist != null)
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
        var allGenres = await _context.PieceLookup.Where(p => p.Genre != null)
            .Select(p => p.Genre!).Distinct().OrderBy(g => g).ToListAsync();
        var allArtists = await _context.PieceLookup.Where(p => p.Artist != null)
            .Select(p => p.Artist!).Distinct().OrderBy(a => a).ToListAsync();
        var allFolders = await _context.PieceLookup.Where(p => p.SourceFolder != null)
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
