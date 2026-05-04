using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class PlayListsController : Controller
{
    private readonly PigContext _context;

    public PlayListsController(PigContext context) => _context = context;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Browse(string? search, int page = 1, int pageSize = 50)
    {
        var query = _context.List.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => l.Title.ToLower().Contains(search.ToLower()));

        var total = await query.CountAsync();
        var playlists = await query.OrderBy(l => l.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var listIds = playlists.Select(p => p.ListId).ToList();
        var counts = await _context.ListFilter
            .Where(lf => listIds.Contains(lf.ListId))
            .GroupBy(lf => lf.ListId)
            .Select(g => new { ListId = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = playlists.Select(p => new {
            p.ListId, p.Title, p.Minimum,
            SongCount = counts.FirstOrDefault(c => c.ListId == p.ListId)?.Count ?? 0
        });

        return Json(new { total, page, pageSize, playlists = result });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var playlist = await _context.List.FindAsync(id);
        if (playlist == null) return NotFound();

        var filters = await _context.ListFilter
            .Where(lf => lf.ListId == id)
            .ToListAsync();

        // Resolve full playlist using cached PieceIds — instant lookup
        var resolvedPieceIds = await PlaylistResolver.ResolvePieceIds(_context, new List<int> { id });
        var pieces = await _context.PieceLookup
            .Where(p => resolvedPieceIds.Contains(p.PieceId))
            .OrderBy(p => p.Artist).ThenBy(p => p.Title)
            .Select(p => new { p.PieceId, p.AudioHash, p.Artist, p.Title, p.Album, p.Genre, p.Year, p.BPM, p.Seconds })
            .ToListAsync();

        var songs = pieces.Select(p => {
            var f = filters.FirstOrDefault(lf => lf.AudioHash == p.AudioHash);
            return new {
                p.PieceId, p.AudioHash, p.Artist, p.Title, p.Album, p.Genre, p.Year, p.BPM, p.Seconds,
                HasTitle = f?.HasTitle ?? false,
                HasArtist = f?.HasArtist ?? false,
                ByArtistExpansion = f == null // song is here because of artist expansion, not a direct filter
            };
        });

        return Json(new { playlist = new { playlist.ListId, playlist.Title, playlist.Minimum }, songs });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveSong([FromBody] RemoveSongRequest req)
    {
        var filter = await _context.ListFilter
            .FirstOrDefaultAsync(lf => lf.ListId == req.ListId && lf.AudioHash == req.AudioHash);
        if (filter == null) return NotFound();
        _context.ListFilter.Remove(filter);
        await _context.SaveChangesAsync();
        await PlaylistResolver.RebuildPlaylist(_context, req.ListId);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFilter([FromBody] ToggleFilterRequest req)
    {
        var filter = await _context.ListFilter
            .FirstOrDefaultAsync(lf => lf.ListId == req.ListId && lf.AudioHash == req.AudioHash);
        if (filter == null) return NotFound();
        if (req.Field == "title") filter.HasTitle = req.Value;
        else if (req.Field == "artist") filter.HasArtist = req.Value;
        filter.Editor = "PlayLists";
        filter.Edited = DateTime.Now;
        await _context.SaveChangesAsync();
        await PlaylistResolver.RebuildPlaylist(_context, req.ListId);
        return Json(new { success = true });
    }
}

public class RemoveSongRequest
{
    public int ListId { get; set; }
    public string AudioHash { get; set; } = "";
}

public class ToggleFilterRequest
{
    public int ListId { get; set; }
    public string AudioHash { get; set; } = "";
    public string Field { get; set; } = "";
    public bool Value { get; set; }
}
