using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class ExportController : Controller
{
    private readonly PigContext _context;

    public ExportController(PigContext context) => _context = context;

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Unfoldered()
    {
        var songs = await _context.PieceLookup
            .Where(p => p.SourceFolder == null || p.SourceFolder == "")
            .OrderBy(p => p.Artist).ThenBy(p => p.Title)
            .Select(p => new { p.PieceId, p.Title, p.Artist, p.FileName })
            .ToListAsync();
        return Json(songs);
    }

    [HttpPost]
    public async Task<IActionResult> AssignFolder([FromBody] AssignFolderRequest req)
    {
        var pieces = await _context.Piece
            .Where(p => req.PieceIds.Contains(p.PieceId))
            .ToListAsync();
        foreach (var p in pieces)
        {
            p.SourceFolder = req.Folder;
            p.Editor = "Export";
            p.Edited = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, count = pieces.Count });
    }

    [HttpGet]
    public async Task<IActionResult> Options()
    {
        var folders = await _context.PieceLookup.Where(p => p.SourceFolder != null && p.SourceFolder != "")
            .Select(p => p.SourceFolder!).Distinct().OrderBy(f => f).ToListAsync();
        var genres = await _context.PieceLookup.Where(p => p.Genre != null && p.Genre != "")
            .Select(p => p.Genre!).Distinct().OrderBy(g => g).ToListAsync();
        var totalSongs = await _context.PieceLookup.CountAsync();
        var playlistCount = await _context.List.CountAsync();
        return Json(new { folders, genres, totalSongs, playlistCount });
    }

    /// <summary>Write one song's MP3 to disk. Returns the file path.</summary>
    private async Task WriteSongToDisk(int pieceId, string destDir)
    {
        var piece = await _context.Piece
            .Where(p => p.PieceId == pieceId)
            .Select(p => new { p.FileName, p.Mp3 })
            .FirstOrDefaultAsync();
        if (piece?.Mp3 == null) return;

        Directory.CreateDirectory(destDir);
        var filePath = Path.Combine(destDir, piece.FileName);
        await System.IO.File.WriteAllBytesAsync(filePath, piece.Mp3);
    }

    /// <summary>Zip a folder using the system zip command, then stream it.</summary>
    private IActionResult ZipAndStream(string sourceDir, string zipFileName, bool compress = false)
    {
        var ext = compress ? ".tar.gz" : ".tar";
        var flag = compress ? "-czf" : "-cf";
        var tarPath = sourceDir + ext;
        var psi = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"{flag} \"{tarPath}\" -C \"{sourceDir}\" .",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit();

        try { Directory.Delete(sourceDir, true); } catch { }

        if (!System.IO.File.Exists(tarPath))
            return StatusCode(500, "Archive creation failed.");

        var contentType = compress ? "application/gzip" : "application/x-tar";
        var dlName = compress ? zipFileName.Replace(".tar", ".tar.gz") : zipFileName.Replace(".zip", ".tar");
        var stream = new FileStream(tarPath, FileMode.Open, FileAccess.Read,
            FileShare.None, 4096, FileOptions.DeleteOnClose);
        return File(stream, contentType, dlName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportAll()
    {
        var exportDir = Path.Combine(Path.GetTempPath(), $"pig_export_{Guid.NewGuid()}");
        var musicDir = Path.Combine(exportDir, "Music");

        // Step 1: Get all song IDs with their folders
        var songs = await _context.PieceLookup
            .Select(p => new { p.PieceId, p.SourceFolder })
            .ToListAsync();

        // Step 2: Write each song to disk one at a time
        foreach (var s in songs)
        {
            var folder = string.IsNullOrEmpty(s.SourceFolder) ? "Unsorted" : s.SourceFolder;
            await WriteSongToDisk(s.PieceId, Path.Combine(musicDir, folder));
        }

        // Step 3: Write m3u files
        await WritePlaylistM3us(musicDir);

        // Step 4: Zip and stream
        return ZipAndStream(exportDir, $"{DateTime.Now:yyyyMMMdd}_PIG_Export.zip");
    }

    [HttpGet]
    public async Task<IActionResult> ExportFolder(string folder)
    {
        var exportDir = Path.Combine(Path.GetTempPath(), $"pig_folder_{Guid.NewGuid()}");
        var songIds = await _context.PieceLookup
            .Where(p => p.SourceFolder == folder)
            .Select(p => p.PieceId).ToListAsync();

        foreach (var id in songIds)
            await WriteSongToDisk(id, Path.Combine(exportDir, folder));

        return ZipAndStream(exportDir, $"{DateTime.Now:yyyyMMMdd}_PIG_{folder}.zip");
    }

    [HttpGet]
    public async Task<IActionResult> ExportGenre(string genre)
    {
        var exportDir = Path.Combine(Path.GetTempPath(), $"pig_genre_{Guid.NewGuid()}");
        var songIds = await _context.PieceLookup
            .Where(p => p.Genre == genre)
            .Select(p => p.PieceId).ToListAsync();

        foreach (var id in songIds)
            await WriteSongToDisk(id, Path.Combine(exportDir, genre));

        return ZipAndStream(exportDir, $"{DateTime.Now:yyyyMMMdd}_PIG_{genre}.zip");
    }

    [HttpGet]
    public async Task<IActionResult> ExportArtist(string artist)
    {
        var exportDir = Path.Combine(Path.GetTempPath(), $"pig_artist_{Guid.NewGuid()}");
        var safeName = string.Join("_", artist.Split(Path.GetInvalidFileNameChars()));
        var songIds = await _context.PieceLookup
            .Where(p => p.Artist == artist)
            .Select(p => p.PieceId).ToListAsync();

        foreach (var id in songIds)
            await WriteSongToDisk(id, Path.Combine(exportDir, safeName));

        return ZipAndStream(exportDir, $"{DateTime.Now:yyyyMMMdd}_PIG_{safeName}.zip");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPlaylists()
    {
        var exportDir = Path.Combine(Path.GetTempPath(), $"pig_playlists_{Guid.NewGuid()}");
        var musicDir = Path.Combine(exportDir, "Music");
        Directory.CreateDirectory(musicDir);
        await WritePlaylistM3us(musicDir);
        return ZipAndStream(exportDir, $"{DateTime.Now:yyyyMMMdd}_PIG_Playlists.tar.gz", compress: true);
    }

    private async Task WritePlaylistM3us(string musicDir)
    {
        var sep = Path.DirectorySeparatorChar;
        var playlists = await _context.List.OrderBy(l => l.Title).ToListAsync();

        foreach (var pl in playlists)
        {
            var resolvedPieceIds = await PlaylistResolver.ResolvePieceIds(_context, new List<int> { pl.ListId });

            var songs = await _context.PieceLookup
                .Where(p => resolvedPieceIds.Contains(p.PieceId))
                .OrderBy(p => p.Artist).ThenBy(p => p.Title)
                .Select(p => new { p.FileName, p.SourceFolder, p.Seconds, p.Artist, p.Title })
                .ToListAsync();

            if (songs.Count == 0) continue;

            var m3u = "#EXTM3U\n";
            foreach (var s in songs)
            {
                var folder = string.IsNullOrEmpty(s.SourceFolder) ? "Unsorted" : s.SourceFolder;
                m3u += $"#EXTINF:{s.Seconds ?? -1},{s.Artist} - {s.Title}\n";
                m3u += $"Music{sep}{folder}{sep}{s.FileName}\n";
            }

            var safeName = string.Join("_", pl.Title.Split(Path.GetInvalidFileNameChars()));
            await System.IO.File.WriteAllTextAsync(Path.Combine(musicDir, $"{safeName}.m3u"), m3u);
        }
    }
}

public class AssignFolderRequest
{
    public List<int> PieceIds { get; set; } = new();
    public string Folder { get; set; } = "";
}
