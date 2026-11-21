using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace PIGv4.Models;
/// <summary>
/// Finds album art online for songs that have no embedded art.
/// Order: MusicBrainz/Cover Art Archive (album match, then the artist's album closest to the song's year),
/// then Wikipedia (album article, then a photo of the musical artist).
/// MusicBrainz, Cover Art Archive, Wikipedia and Wikidata are free and need no API key;
/// MusicBrainz asks for at most 1 request per second and a User-Agent with contact info.
/// </summary>
public static class AlbumArtLookup
{
    /// <summary>Appended to URLs that are a photo of the artist rather than album art.</summary>
    public const string ArtistPhotoMarker = "#artist";
    private const string UserAgent = "PIGv4/1.0 (https://github.com/Zodiac1913/PIGv4)";
    private const int MaxUrlLength = 500;
    private static readonly HttpClient _http = CreateClient();
    private static readonly SemaphoreSlim _mbGate = new(1, 1);
    private static DateTime _mbLastCall = DateTime.MinValue;
    private static readonly ConcurrentDictionary<int, byte> _inFlight = new();
    private static readonly Regex MusicianWords = new(
        @"\b(musician|singer|band|rapper|songwriter|group|duo|trio|quartet|vocalist|guitarist|pianist|drummer|bassist|composer|dj|disc jockey|record producer|rock|pop|hip hop|country|jazz|metal|punk|rapper|orchestra|ensemble|musical)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AlbumWords = new(
        @"\b(album|ep|soundtrack|compilation|mixtape|live album|extended play)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        c.DefaultRequestHeaders.Add("Accept", "application/json");
        return c;
    }
    /// <summary>Returns false if a lookup for this piece is already running.</summary>
    public static bool TryBegin(int pieceId) => _inFlight.TryAdd(pieceId, 0);
    public static void End(int pieceId) => _inFlight.TryRemove(pieceId, out _);
    public static bool IsRunning(int pieceId) => _inFlight.ContainsKey(pieceId);
    /// <summary>Find the best art URL, skipping any URL in <paramref name="rejected"/>. Null if nothing found.</summary>
    public static async Task<string?> FindAsync(string? artist, string? album, int? year, ISet<string> rejected)
    {
        artist = CleanArtist(artist);
        if (string.IsNullOrEmpty(artist)) return null;
        album = string.IsNullOrWhiteSpace(album) ? null : album.Trim();
        int? songYear = year is > 0 ? year : null;
        bool Ok(string? url) => !string.IsNullOrEmpty(url) && url.Length <= MaxUrlLength && !rejected.Contains(url);
        // 1. Resolve the artist on MusicBrainz (this is what keeps "Prince" from becoming Prince William)
        var mbid = await ResolveMbArtistAsync(artist);
        // 2. Album match on MusicBrainz → Cover Art Archive, closest to the song's year
        if (album != null)
        {
            foreach (var rgId in await FindReleaseGroupsByAlbumAsync(artist, mbid, album, songYear))
            {
                var url = await CoverArtUrlAsync(rgId);
                if (Ok(url)) return url;
            }
        }
        // 3. No album match: the artist's album closest to the song's year
        if (mbid != null && songYear != null)
        {
            foreach (var rgId in await FindArtistAlbumsByYearAsync(mbid, songYear.Value))
            {
                var url = await CoverArtUrlAsync(rgId);
                if (Ok(url)) return url;
            }
        }
        // 4. Wikipedia: find the musical artist's article
        var artistPage = await ResolveWikipediaArtistAsync(artist, mbid);
        // 5. Wikipedia album article cover
        if (album != null)
        {
            var url = await WikipediaAlbumCoverAsync(album, artist, artistPage?.Title);
            if (Ok(url)) return url;
        }
        // 6. Last resort: photo of the artist (marked so the UI does not call it album art)
        if (artistPage?.ImageUrl != null)
        {
            var url = artistPage.ImageUrl + ArtistPhotoMarker;
            if (Ok(url)) return url;
        }
        return null;
    }
    // ── MusicBrainz ─────────────────────────────────────────────────
    private static async Task<JsonDocument?> MbGetAsync(string url)
    {
        await _mbGate.WaitAsync();
        try
        {
            var wait = _mbLastCall.AddMilliseconds(1100) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            _mbLastCall = DateTime.UtcNow;
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            return JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        }
        catch { return null; }
        finally { _mbGate.Release(); }
    }
    private static async Task<string?> ResolveMbArtistAsync(string artist)
    {
        using var doc = await MbGetAsync(
            $"https://musicbrainz.org/ws/2/artist/?query=artist:{Q(artist)}&fmt=json&limit=10");
        if (doc == null || !doc.RootElement.TryGetProperty("artists", out var artists)) return null;
        var target = Norm(artist);
        foreach (var a in artists.EnumerateArray())
        {
            var score = a.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
            var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (score >= 90 && Norm(name) == target && a.TryGetProperty("id", out var id))
                return id.GetString();
        }
        return null;
    }
    private static async Task<List<string>> FindReleaseGroupsByAlbumAsync(string artist, string? mbid, string album, int? year)
    {
        var artistClause = mbid != null ? $"arid:{mbid}" : $"artist:{Q(artist)}";
        using var doc = await MbGetAsync(
            $"https://musicbrainz.org/ws/2/release-group/?query=releasegroup:{Q(album)}%20AND%20{artistClause}&fmt=json&limit=10");
        if (doc == null || !doc.RootElement.TryGetProperty("release-groups", out var rgs)) return new();
        var target = Norm(album);
        var matches = new List<(string id, int? year, bool exact, int score)>();
        foreach (var rg in rgs.EnumerateArray())
        {
            var score = rg.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
            if (score < 80 || !rg.TryGetProperty("id", out var id)) continue;
            var title = rg.TryGetProperty("title", out var t) ? t.GetString() : null;
            matches.Add((id.GetString()!, YearOf(rg, "first-release-date"), Norm(title) == target, score));
        }
        return matches
            .OrderByDescending(m => m.exact)
            .ThenBy(m => year == null || m.year == null ? 0 : Math.Abs(m.year.Value - year.Value))
            .ThenByDescending(m => m.score)
            .Take(5).Select(m => m.id).ToList();
    }
    private static async Task<List<string>> FindArtistAlbumsByYearAsync(string mbid, int year)
    {
        using var doc = await MbGetAsync(
            $"https://musicbrainz.org/ws/2/release-group?artist={mbid}&type=album&fmt=json&limit=100");
        if (doc == null || !doc.RootElement.TryGetProperty("release-groups", out var rgs)) return new();
        return rgs.EnumerateArray()
            .Select(rg => (id: rg.TryGetProperty("id", out var id) ? id.GetString() : null, year: YearOf(rg, "first-release-date")))
            .Where(x => x.id != null && x.year != null)
            .OrderBy(x => Math.Abs(x.year!.Value - year))
            .ThenBy(x => x.year)
            .Take(5).Select(x => x.id!).ToList();
    }
    /// <summary>Cover Art Archive front cover for a release group, or null if it has none.</summary>
    private static async Task<string?> CoverArtUrlAsync(string releaseGroupId)
    {
        var url = $"https://coverartarchive.org/release-group/{releaseGroupId}/front-250";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode ? url : null;
        }
        catch { return null; }
    }
    // ── Wikipedia ───────────────────────────────────────────────────
    private record WikiPage(string Title, string? Description, string? Extract, string? ImageUrl);
    private static async Task<WikiPage?> WikiSummaryAsync(string title)
    {
        try
        {
            var url = "https://en.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title.Replace(' ', '_'));
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var r = doc.RootElement;
            if (r.TryGetProperty("type", out var type) && type.GetString() == "disambiguation") return null;
            string? img = null;
            if (r.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var src)) img = src.GetString();
            return new WikiPage(
                r.TryGetProperty("title", out var t) ? t.GetString() ?? title : title,
                r.TryGetProperty("description", out var d) ? d.GetString() : null,
                r.TryGetProperty("extract", out var e) ? e.GetString() : null,
                img);
        }
        catch { return null; }
    }
    private static bool IsMusicalArtist(WikiPage p) =>
        MusicianWords.IsMatch(p.Description ?? "") ||
        (string.IsNullOrEmpty(p.Description) && MusicianWords.IsMatch(p.Extract ?? ""));
    private static async Task<WikiPage?> ResolveWikipediaArtistAsync(string artist, string? mbid)
    {
        // Best: MusicBrainz → Wikidata → English Wikipedia title (exact, no guessing)
        if (mbid != null)
        {
            var title = await WikipediaTitleFromMbAsync(mbid);
            if (title != null)
            {
                var page = await WikiSummaryAsync(title);
                if (page != null) return page;
            }
        }
        // Fallback: try music-specific titles first, accept only pages that describe a musical artist
        foreach (var candidate in new[] { $"{artist} (musician)", $"{artist} (band)", $"{artist} (singer)", $"{artist} (rapper)", artist })
        {
            var page = await WikiSummaryAsync(candidate);
            if (page != null && IsMusicalArtist(page)) return page;
        }
        return null;
    }
    private static async Task<string?> WikipediaTitleFromMbAsync(string mbid)
    {
        using var doc = await MbGetAsync($"https://musicbrainz.org/ws/2/artist/{mbid}?inc=url-rels&fmt=json");
        if (doc == null || !doc.RootElement.TryGetProperty("relations", out var rels)) return null;
        foreach (var rel in rels.EnumerateArray())
        {
            var type = rel.TryGetProperty("type", out var t) ? t.GetString() : null;
            var resource = rel.TryGetProperty("url", out var u) && u.TryGetProperty("resource", out var r) ? r.GetString() : null;
            if (resource == null) continue;
            if (type == "wikipedia" && resource.Contains("en.wikipedia.org/wiki/"))
                return Uri.UnescapeDataString(resource[(resource.IndexOf("/wiki/") + 6)..]);
            if (type == "wikidata")
            {
                var qid = resource[(resource.LastIndexOf('/') + 1)..];
                try
                {
                    using var resp = await _http.GetAsync($"https://www.wikidata.org/wiki/Special:EntityData/{qid}.json");
                    if (!resp.IsSuccessStatusCode) continue;
                    using var wd = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    if (wd.RootElement.TryGetProperty("entities", out var ents))
                        foreach (var ent in ents.EnumerateObject())
                            if (ent.Value.TryGetProperty("sitelinks", out var sl) && sl.TryGetProperty("enwiki", out var en)
                                && en.TryGetProperty("title", out var title))
                                return title.GetString();
                }
                catch { }
            }
        }
        return null;
    }
    private static async Task<string?> WikipediaAlbumCoverAsync(string album, string artist, string? artistTitle)
    {
        var names = new List<string> { artist };
        if (artistTitle != null) names.Add(Regex.Replace(artistTitle, @"\s*\(.*\)$", ""));
        var candidates = new List<string> { $"{album} ({artist} album)" };
        if (artistTitle != null && artistTitle != artist) candidates.Add($"{album} ({artistTitle} album)");
        candidates.Add($"{album} (album)");
        candidates.Add(album);
        foreach (var c in candidates.Distinct())
        {
            var page = await WikiSummaryAsync(c);
            if (page?.ImageUrl == null) continue;
            var text = (page.Description ?? "") + " " + (page.Extract ?? "");
            if (AlbumWords.IsMatch(page.Description ?? "") &&
                names.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase)))
                return page.ImageUrl;
        }
        return null;
    }
    // ── Helpers ─────────────────────────────────────────────────────
    private static string Q(string s) => Uri.EscapeDataString("\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
    private static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.ToLowerInvariant().Replace("&", "and");
        s = Regex.Replace(s, @"^the\s+", "");
        return Regex.Replace(s, @"[^\p{L}\p{N}]+", "");
    }
    /// <summary>"Prince feat. X" → "Prince".</summary>
    private static string? CleanArtist(string? artist)
    {
        if (string.IsNullOrWhiteSpace(artist)) return null;
        return Regex.Split(artist, @"\s+(feat\.?|ft\.?|featuring)\s+", RegexOptions.IgnoreCase)[0].Trim();
    }
    private static int? YearOf(JsonElement e, string prop)
    {
        var d = e.TryGetProperty(prop, out var v) ? v.GetString() : null;
        return d != null && d.Length >= 4 && int.TryParse(d[..4], out var y) ? y : null;
    }
}
