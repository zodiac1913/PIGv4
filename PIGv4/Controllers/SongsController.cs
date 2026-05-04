using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class SongsController : Controller
{
    private readonly PigContext _context;

    public SongsController(PigContext context) => _context = context;

    public IActionResult Index() => View();

    /// <summary>Browse songs with search, folder filter, and new-only toggle.</summary>
    [HttpGet]
    public async Task<IActionResult> Browse(string? search, string? startsWith, string? folder, bool? newOnly,
        int page = 1, int pageSize = 50)
    {
        var query = _context.PieceLookup.AsQueryable();

        if (!string.IsNullOrWhiteSpace(folder))
            query = query.Where(p => p.SourceFolder == folder);
        if (newOnly == true)
            query = query.Where(p => p.IsNew);
        if (!string.IsNullOrWhiteSpace(startsWith))
            query = query.Where(p => p.Artist != null && p.Artist.ToLower().StartsWith(startsWith.ToLower()));
        else if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                (p.Title != null && p.Title.ToLower().Contains(search.ToLower())) ||
                (p.Artist != null && p.Artist.ToLower().Contains(search.ToLower())) ||
                (p.Album != null && p.Album.ToLower().Contains(search.ToLower())) ||
                (p.FileName != null && p.FileName.ToLower().Contains(search.ToLower())));

        var total = await query.CountAsync();
        var songs = await query
            .OrderBy(p => p.Artist).ThenBy(p => p.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.PieceId, p.AudioHash, p.Title, p.Artist, p.Album,
                p.Genre, p.Year, p.BPM, p.Seconds, p.SourceFolder,
                p.FileName, p.IsNew
            })
            .ToListAsync();

        return Json(new { total, page, pageSize, songs });
    }

    /// <summary>Get a single song's full detail + its playlist assignments.</summary>
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var piece = await _context.PieceLookup
            .Where(p => p.PieceId == id)
            .Select(p => new
            {
                p.PieceId, p.AudioHash, p.Title, p.Artist, p.Album,
                p.Genre, p.Year, p.BPM, p.Seconds, p.SourceFolder,
                p.FileName, p.FileSize, p.IsNew
            })
            .FirstOrDefaultAsync();

        if (piece == null) return NotFound();

        // Get all playlists
        var playlists = await _context.List
            .OrderBy(l => l.Title)
            .Select(l => new { l.ListId, l.Title })
            .ToListAsync();

        // Get this song's filter assignments
        var filters = await _context.ListFilter
            .Where(lf => lf.AudioHash == piece.AudioHash)
            .Select(lf => new { lf.ListFilterId, lf.ListId, lf.HasArtist, lf.HasTitle, lf.HasGenre })
            .ToListAsync();

        // Check if this artist has HasArtist=true on ANY song for each playlist
        List<object>? artistFlags = null;
        if (!string.IsNullOrEmpty(piece.Artist))
        {
            var artistHashes = await _context.PieceLookup
                .Where(p => p.Artist == piece.Artist)
                .Select(p => p.AudioHash).Distinct().ToListAsync();

            artistFlags = await _context.ListFilter
                .Where(lf => artistHashes.Contains(lf.AudioHash) && lf.HasArtist == true && lf.AudioHash != piece.AudioHash)
                .Select(lf => new { lf.ListId, lf.AudioHash })
                .Distinct()
                .ToListAsync<object>();

            // Get the song names for tooltip
            var artistFlagDetails = await _context.ListFilter
                .Where(lf => artistHashes.Contains(lf.AudioHash) && lf.HasArtist == true && lf.AudioHash != piece.AudioHash)
                .Join(_context.PieceLookup, lf => lf.AudioHash, p => p.AudioHash, (lf, p) => new { lf.ListId, p.Title })
                .ToListAsync();

            artistFlags = artistFlagDetails.Select(a => (object)new { a.ListId, a.Title }).ToList();
        }

        return Json(new { piece, playlists, filters, artistFlags });
    }

    /// <summary>Save MP3 tag edits for a song.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveTags([FromBody] SaveTagsRequest req)
    {
        var piece = await _context.Piece.FindAsync(req.PieceId);
        if (piece == null) return NotFound();

        piece.Title = req.Title?.Trim();
        piece.Artist = req.Artist?.Trim();
        piece.Album = req.Album?.Trim();
        piece.Genre = req.Genre?.Trim();
        piece.Year = req.Year;
        piece.BPM = req.BPM;
        piece.Editor = "Songs";
        piece.Edited = DateTime.Now;

        // Write tags into the MP3 blob
        if (piece.Mp3 != null)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), piece.FileName);
                await System.IO.File.WriteAllBytesAsync(tempPath, piece.Mp3);
                using var tagFile = TagLib.File.Create(tempPath);
                tagFile.Tag.Title = piece.Title;
                tagFile.Tag.Performers = piece.Artist != null ? new[] { piece.Artist } : Array.Empty<string>();
                tagFile.Tag.Album = piece.Album;
                tagFile.Tag.Genres = piece.Genre != null ? new[] { piece.Genre } : Array.Empty<string>();
                tagFile.Tag.Year = piece.Year.HasValue ? (uint)piece.Year.Value : 0;
                tagFile.Tag.BeatsPerMinute = piece.BPM.HasValue ? (uint)piece.BPM.Value : 0;
                tagFile.Save();
                piece.Mp3 = await System.IO.File.ReadAllBytesAsync(tempPath);
                System.IO.File.Delete(tempPath);
            }
            catch { }
        }

        await _context.SaveChangesAsync();
        await PlaylistResolver.UpdateLookup(_context, piece);
        return Json(new { success = true });
    }

    /// <summary>Save playlist filter assignments for a song and mark it as not new.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveFilters([FromBody] SaveFiltersRequest req)
    {
        var piece = await _context.Piece.FindAsync(req.PieceId);
        if (piece == null) return NotFound();

        // Remove existing filters for this song
        var existing = await _context.ListFilter
            .Where(lf => lf.AudioHash == piece.AudioHash)
            .ToListAsync();
        _context.ListFilter.RemoveRange(existing);

        // Add new ones
        foreach (var f in req.Filters)
        {
            if (!f.HasTitle && !f.HasArtist) continue; // skip unchecked
            _context.ListFilter.Add(new ListFilter
            {
                ListId = f.ListId,
                ListUniqueId = Guid.Empty,
                AudioHash = piece.AudioHash,
                HasTitle = f.HasTitle,
                HasArtist = f.HasArtist,
                HasGenre = false,
                Creator = "Songs"
            });
        }

        // Mark as no longer new
        piece.IsNew = false;
        piece.Editor = "Songs";
        piece.Edited = DateTime.Now;

        await _context.SaveChangesAsync();

        // Rebuild cache for affected playlists only
        var affectedListIds = existing.Select(lf => lf.ListId)
            .Union(req.Filters.Where(f => f.HasTitle || f.HasArtist).Select(f => f.ListId))
            .Distinct();
        await PlaylistResolver.RebuildPlaylists(_context, affectedListIds);

        return Json(new { success = true });
    }

    /// <summary>Get folder list for the filter dropdown.</summary>
    [HttpGet]
    public async Task<IActionResult> Folders()
    {
        var folders = await _context.PieceLookup
            .Where(p => p.SourceFolder != null)
            .Select(p => p.SourceFolder!).Distinct().OrderBy(f => f).ToListAsync();
        return Json(folders);
    }

    /// <summary>Download a song's MP3 file.</summary>
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var piece = await _context.Piece
            .Where(p => p.PieceId == id)
            .Select(p => new { p.Mp3, p.FileName })
            .FirstOrDefaultAsync();
        if (piece?.Mp3 == null) return NotFound();
        return File(piece.Mp3, "audio/mpeg", piece.FileName);
    }

    /// <summary>Delete a song and its filter assignments.</summary>
    [HttpPost]
    public async Task<IActionResult> Delete([FromBody] DeleteRequest req)
    {
        var piece = await _context.Piece.FindAsync(req.PieceId);
        if (piece == null) return NotFound();

        // Remove filter assignments
        var filters = await _context.ListFilter
            .Where(lf => lf.AudioHash == piece.AudioHash)
            .ToListAsync();
        _context.ListFilter.RemoveRange(filters);

        _context.Piece.Remove(piece);
        await _context.SaveChangesAsync();

        // Update caches
        await PlaylistResolver.RemoveLookup(_context, req.PieceId);
        var affectedListIds = filters.Select(lf => lf.ListId).Distinct();
        await PlaylistResolver.RebuildPlaylists(_context, affectedListIds);

        return Json(new { success = true });
    }
}

public class SaveTagsRequest
{
    public int PieceId { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int? BPM { get; set; }
}

public class SaveFiltersRequest
{
    public int PieceId { get; set; }
    public List<FilterAssignment> Filters { get; set; } = new();
}

public class FilterAssignment
{
    public int ListId { get; set; }
    public bool HasTitle { get; set; }
    public bool HasArtist { get; set; }
}

public class DeleteRequest
{
    public int PieceId { get; set; }
}
