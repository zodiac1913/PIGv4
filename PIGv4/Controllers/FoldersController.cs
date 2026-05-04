using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class FoldersController : Controller
{
    private readonly PigContext _context;

    public FoldersController(PigContext context) => _context = context;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Browse()
    {
        var folders = await _context.PieceLookup
            .GroupBy(p => p.SourceFolder ?? "(No Folder)")
            .Select(g => new { Folder = g.Key, SongCount = g.Count() })
            .OrderBy(f => f.Folder)
            .ToListAsync();
        return Json(folders);
    }

    [HttpGet]
    public async Task<IActionResult> Songs(string folder)
    {
        var query = _context.PieceLookup.AsQueryable();
        if (folder == "(No Folder)")
            query = query.Where(p => p.SourceFolder == null || p.SourceFolder == "");
        else
            query = query.Where(p => p.SourceFolder == folder);

        var songs = await query
            .OrderBy(p => p.Artist).ThenBy(p => p.Title)
            .Select(p => new { p.PieceId, p.Artist, p.Title, p.Album, p.Genre, p.FileName })
            .ToListAsync();
        return Json(songs);
    }

    [HttpPost]
    public async Task<IActionResult> MoveSongs([FromBody] MoveSongsRequest req)
    {
        var pieces = await _context.Piece
            .Where(p => req.PieceIds.Contains(p.PieceId))
            .ToListAsync();
        foreach (var p in pieces)
        {
            p.SourceFolder = req.TargetFolder;
            p.Editor = "Folders";
            p.Edited = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        foreach (var p in pieces) await PlaylistResolver.UpdateLookup(_context, p);
        return Json(new { success = true, count = pieces.Count });
    }

    [HttpPost]
    public async Task<IActionResult> RenameFolder([FromBody] RenameFolderRequest req)
    {
        var pieces = await _context.Piece
            .Where(p => p.SourceFolder == req.OldName)
            .ToListAsync();
        foreach (var p in pieces)
        {
            p.SourceFolder = req.NewName;
            p.Editor = "Folders";
            p.Edited = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        foreach (var p in pieces) await PlaylistResolver.UpdateLookup(_context, p);
        return Json(new { success = true, count = pieces.Count });
    }
}

public class MoveSongsRequest
{
    public List<int> PieceIds { get; set; } = new();
    public string TargetFolder { get; set; } = "";
}

public class RenameFolderRequest
{
    public string OldName { get; set; } = "";
    public string NewName { get; set; } = "";
}
