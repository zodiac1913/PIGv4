using System.Diagnostics;
using System.Text.Json;

namespace PIGv4.Models;

public class MusicBrainzResult
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public int? Track { get; set; }
    public string? RecordingId { get; set; }
    public double Score { get; set; }
}

public static class MusicBrainzLookup
{
    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "PIGv4/1.0 (playlist-intelligent-generator)" }
        }
    };

    private static (string? fingerprint, int duration) GetFingerprint(string filePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "fpcalc",
                Arguments = $"-json \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (null, 0);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30000);
            if (process.ExitCode != 0) return (null, 0);

            using var doc = JsonDocument.Parse(output);
            var fp = doc.RootElement.GetProperty("fingerprint").GetString();
            var dur = (int)doc.RootElement.GetProperty("duration").GetDouble();
            return (fp, dur);
        }
        catch { return (null, 0); }
    }

    /// <summary>
    /// Looks up a file against AcoustID → MusicBrainz.
    /// Then enriches with a direct MusicBrainz recording query for genre/track.
    /// </summary>
    public static async Task<MusicBrainzResult?> LookupAsync(string filePath, string apiKey)
    {
        var (fingerprint, duration) = GetFingerprint(filePath);
        if (fingerprint == null) return null;

        // Step 1: AcoustID lookup
        var acoustUrl = $"https://api.acoustid.org/v2/lookup?client={apiKey}"
            + $"&duration={duration}&fingerprint={fingerprint}"
            + "&meta=recordings+releasegroups+compress";

        string acoustJson;
        try { acoustJson = await _http.GetStringAsync(acoustUrl); }
        catch { return null; }

        using var acoustDoc = JsonDocument.Parse(acoustJson);
        var root = acoustDoc.RootElement;

        if (root.GetProperty("status").GetString() != "ok") return null;
        var results = root.GetProperty("results");
        if (results.GetArrayLength() == 0) return null;

        var best = results[0];
        var score = best.TryGetProperty("score", out var s) ? s.GetDouble() : 0;

        if (!best.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
            return null;

        var recording = recordings[0];
        var title = recording.TryGetProperty("title", out var t) ? t.GetString() : null;
        var recordingId = recording.TryGetProperty("id", out var rid) ? rid.GetString() : null;

        string? artist = null;
        if (recording.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0)
            artist = artists[0].TryGetProperty("name", out var a) ? a.GetString() : null;

        string? album = null;
        int? year = null;
        if (recording.TryGetProperty("releasegroups", out var rgs) && rgs.GetArrayLength() > 0)
        {
            var rg = rgs[0];
            album = rg.TryGetProperty("title", out var at) ? at.GetString() : null;

            if (rg.TryGetProperty("releases", out var releases) && releases.GetArrayLength() > 0)
            {
                var dateObj = releases[0].TryGetProperty("date", out var d) ? d : default;
                if (dateObj.ValueKind == JsonValueKind.Object && dateObj.TryGetProperty("year", out var y))
                    year = y.GetInt32();
            }
        }

        var result = new MusicBrainzResult
        {
            Title = title,
            Artist = artist,
            Album = album,
            Year = year,
            Score = score,
            RecordingId = recordingId
        };

        // Step 2: Enrich from MusicBrainz API if we have a recording ID
        if (!string.IsNullOrEmpty(recordingId))
        {
            try
            {
                await RateLimitDelay();
                var mbUrl = $"https://musicbrainz.org/ws/2/recording/{recordingId}"
                    + "?inc=genres+releases&fmt=json";
                var mbJson = await _http.GetStringAsync(mbUrl);
                using var mbDoc = JsonDocument.Parse(mbJson);
                var mbRoot = mbDoc.RootElement;

                // Genre from tags
                if (mbRoot.TryGetProperty("genres", out var genres) && genres.GetArrayLength() > 0)
                {
                    // Pick the genre with the highest vote count
                    string? bestGenre = null;
                    int bestCount = -1;
                    foreach (var g in genres.EnumerateArray())
                    {
                        var name = g.TryGetProperty("name", out var gn) ? gn.GetString() : null;
                        var count = g.TryGetProperty("count", out var gc) ? gc.GetInt32() : 0;
                        if (name != null && count > bestCount)
                        {
                            bestGenre = name;
                            bestCount = count;
                        }
                    }
                    result.Genre = bestGenre;
                }

                // Track number + year from releases
                if (mbRoot.TryGetProperty("releases", out var mbReleases) && mbReleases.GetArrayLength() > 0)
                {
                    var rel = mbReleases[0];

                    // Year fallback
                    if (result.Year == null && rel.TryGetProperty("date", out var dateStr))
                    {
                        var ds = dateStr.GetString();
                        if (ds != null && ds.Length >= 4 && int.TryParse(ds[..4], out var yr))
                            result.Year = yr;
                    }

                    // Track number from media → tracks
                    if (rel.TryGetProperty("media", out var media) && media.GetArrayLength() > 0)
                    {
                        var disc = media[0];
                        if (disc.TryGetProperty("tracks", out var tracks) && tracks.GetArrayLength() > 0)
                        {
                            // Find the track that matches our recording
                            foreach (var trk in tracks.EnumerateArray())
                            {
                                var trkRecId = trk.TryGetProperty("recording", out var trkRec)
                                    && trkRec.TryGetProperty("id", out var trkId)
                                    ? trkId.GetString() : null;

                                if (trkRecId == recordingId || tracks.GetArrayLength() == 1)
                                {
                                    if (trk.TryGetProperty("position", out var pos))
                                        result.Track = pos.GetInt32();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { /* enrichment is best-effort */ }
        }

        return result;
    }

    public static async Task RateLimitDelay()
    {
        await Task.Delay(1100);
    }
}
