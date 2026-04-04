using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class SettingsController : Controller
{
    private readonly PigContext _context;
    private readonly IConfiguration _config;

    public SettingsController(PigContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public IActionResult Index() => View();

    private string GetDbPath()
    {
        var connStr = _config.GetConnectionString("DefaultConnection") ?? "";
        var path = connStr.Replace("Data Source=", "").Trim();
        if (!Path.IsPathRooted(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), path);
        return Path.GetFullPath(path);
    }

    [HttpGet]
    public IActionResult ExportDb()
    {
        var dbPath = GetDbPath();
        if (!System.IO.File.Exists(dbPath))
            return NotFound("Database file not found.");

        // Force SQLite to flush WAL to main file
        _context.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);");

        // Zip to a temp file (DB is too large for MemoryStream)
        var zipPath = Path.Combine(Path.GetTempPath(), $"pig_backup_{Guid.NewGuid()}.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(dbPath, "pigDb.db", CompressionLevel.Fastest);
        }

        var fileName = $"{DateTime.Now:yyyyMMMdd}_PIG_Backup.zip";
        var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);
        return File(stream, "application/zip", fileName);
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000_000)] // 20GB limit
    public async Task<IActionResult> ImportDb(IFormFile dbFile)
    {
        if (dbFile == null || dbFile.Length == 0)
            return Json(new { success = false, error = "No file provided." });

        if (!dbFile.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            && !dbFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, error = "File must be a .db or .zip file." });

        var dbPath = GetDbPath();
        var backupPath = dbPath + ".pre-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

        try
        {
            // Close the current connection
            await _context.Database.CloseConnectionAsync();

            // Backup current DB first
            if (System.IO.File.Exists(dbPath))
                System.IO.File.Copy(dbPath, backupPath, true);

            // Write the uploaded file (unzip if needed)
            if (dbFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zipStream = dbFile.OpenReadStream();
                using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
                var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".db", StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    return Json(new { success = false, error = "No .db file found in zip." });
                using var entryStream = entry.Open();
                using var fileStream = new FileStream(dbPath, FileMode.Create);
                await entryStream.CopyToAsync(fileStream);
            }
            else
            {
                using var stream = new FileStream(dbPath, FileMode.Create);
                await dbFile.CopyToAsync(stream);
            }

            // Delete WAL/SHM files so SQLite starts fresh
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";
            if (System.IO.File.Exists(walPath)) System.IO.File.Delete(walPath);
            if (System.IO.File.Exists(shmPath)) System.IO.File.Delete(shmPath);

            return Json(new { success = true, backup = Path.GetFileName(backupPath) });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult DbInfo()
    {
        var dbPath = GetDbPath();
        var fileInfo = new FileInfo(dbPath);
        var sizeMb = fileInfo.Exists ? Math.Round(fileInfo.Length / 1024.0 / 1024.0, 1) : 0;
        return Json(new { path = dbPath, sizeMb, lastModified = fileInfo.LastWriteTime });
    }
}
