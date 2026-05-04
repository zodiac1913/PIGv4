using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIGv4.Models;

namespace PIGv4.Controllers;

public class ImpexController : Controller
{
    private readonly PigContext _context;
    private readonly string? _acoustIdKey;

    public ImpexController(PigContext context, IConfiguration config)
    {
        _context = context;
        _acoustIdKey = config["AcoustId:ApiKey"];
    }

    public IActionResult Index() => View();

    [HttpPost]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> ImportFromFiles(List<IFormFile> files, string? sourceFolder)
    {
        if (files == null || files.Count == 0)
            return Json(new { status = "error", reason = "No files received." });

        var file = files.First();
        if (!file.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return Json(new { status = "error", reason = "Not an MP3 file." });

        var config = await _context.Config.FirstOrDefaultAsync();
        var targetDb = config?.TargetDb ?? 89.0;

        try
        {
            var fileName = Path.GetFileName(file.FileName);

            byte[] mp3Bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                mp3Bytes = ms.ToArray();
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp3");
            await System.IO.File.WriteAllBytesAsync(tempPath, mp3Bytes);

            string audioHash;
            string hashMethod = "audio-only";
            try { audioHash = AudioHasher.ComputeHash(tempPath); }
            catch
            {
                audioHash = Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(mp3Bytes));
                hashMethod = "full-file-fallback";
            }

            var existing = await _context.Piece.FirstOrDefaultAsync(p => p.AudioHash == audioHash);
            if (existing != null)
            {
                try { System.IO.File.Delete(tempPath); } catch { }
                return Json(new
                {
                    status = "skipped",
                    reason = $"Duplicate audio hash ({hashMethod}). Matches PieceId {existing.PieceId}: \"{existing.FileName}\"."
                });
            }

            TagLib.File? tagFile = null;
            try { tagFile = TagLib.File.Create(tempPath); } catch { }

            var piece = new Piece
            {
                AudioHash = audioHash,
                Title = (tagFile?.Tag.Title ?? Path.GetFileNameWithoutExtension(fileName)).Trim(),
                Artist = tagFile?.Tag.FirstPerformer?.Trim(),
                Album = tagFile?.Tag.Album?.Trim(),
                Genre = tagFile?.Tag.FirstGenre?.Trim(),
                Year = tagFile?.Tag.Year > 0 ? (int?)tagFile.Tag.Year : null,
                BPM = null,
                Seconds = tagFile?.Properties.Duration.TotalSeconds > 0
                    ? (int?)tagFile.Properties.Duration.TotalSeconds : null,
                FileName = fileName,
                SourceFolder = sourceFolder,
                FileSize = mp3Bytes.Length,
                Mp3 = null,
                Creator = "Import",
                Created = DateTime.Now
            };

            piece.BPM = BpmDetector.Detect(tempPath);

            uint? tagTrack = tagFile?.Tag.Track;
            string? tagComment = tagFile?.Tag.Comment;

            MusicBrainzResult? mbResult = null;
            if (!string.IsNullOrEmpty(_acoustIdKey))
            {
                try
                {
                    mbResult = await MusicBrainzLookup.LookupAsync(tempPath, _acoustIdKey);
                    await MusicBrainzLookup.RateLimitDelay();
                }
                catch { }
            }

            tagFile?.Dispose();

            Mp3Normalizer.Normalize(tempPath, targetDb);
            piece.Mp3 = await System.IO.File.ReadAllBytesAsync(tempPath);
            piece.FileSize = piece.Mp3.Length;

            try { System.IO.File.Delete(tempPath); } catch { }

            _context.Piece.Add(piece);
            await _context.SaveChangesAsync();
            await PlaylistResolver.UpdateLookup(_context, piece);

            return Json(new
            {
                status = "imported",
                song = new
                {
                    pieceId = piece.PieceId,
                    audioHash = piece.AudioHash,
                    fileName,
                    sourceFolder,
                    title = piece.Title,
                    artist = piece.Artist,
                    album = piece.Album,
                    genre = piece.Genre,
                    year = piece.Year,
                    bpm = piece.BPM,
                    seconds = piece.Seconds,
                    fileSize = piece.FileSize,
                    track = tagTrack,
                    comment = tagComment,
                    mb = mbResult != null ? new
                    {
                        title = mbResult.Title,
                        artist = mbResult.Artist,
                        album = mbResult.Album,
                        genre = mbResult.Genre,
                        year = mbResult.Year,
                        track = mbResult.Track,
                        score = mbResult.Score
                    } : null
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { status = "error", reason = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSongTags([FromBody] ImportedSongViewModel model)
    {
        var piece = await _context.Piece.FindAsync(model.SongId);
        if (piece == null) return NotFound();

        piece.Title = model.Title;
        piece.Artist = model.Artist;
        piece.Album = model.Album;
        piece.Genre = model.Genre;
        piece.Year = model.Year;
        piece.BPM = model.BPM;
        piece.Editor = "Import";
        piece.Edited = DateTime.Now;

        if (piece.Mp3 != null)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), piece.FileName);
                await System.IO.File.WriteAllBytesAsync(tempPath, piece.Mp3);

                using var tagFile = TagLib.File.Create(tempPath);
                tagFile.Tag.Title = model.Title;
                tagFile.Tag.Performers = model.Artist != null ? new[] { model.Artist } : Array.Empty<string>();
                tagFile.Tag.Album = model.Album;
                tagFile.Tag.Genres = model.Genre != null ? new[] { model.Genre } : Array.Empty<string>();
                tagFile.Tag.Year = model.Year.HasValue ? (uint)model.Year.Value : 0;
                tagFile.Tag.BeatsPerMinute = model.BPM.HasValue ? (uint)model.BPM.Value : 0;
                tagFile.Tag.Track = model.Track.HasValue ? model.Track.Value : 0;
                tagFile.Tag.Comment = model.Comment;
                tagFile.Save();

                piece.Mp3 = await System.IO.File.ReadAllBytesAsync(tempPath);
                System.IO.File.Delete(tempPath);
            }
            catch { }
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }
}
