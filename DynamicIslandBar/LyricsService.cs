using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;

namespace DynamicIslandBar;

public record LyricLine(TimeSpan Time, string Text);

public class LyricsService
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        UseProxy = false
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private const int MaxRetries = 2;
    private static readonly Regex LrcRegex = new(@"\[(\d{1,2}):(\d{2})[.:](\d{1,3})\](.*)", RegexOptions.Compiled);
    private List<LyricLine> _parsedLyrics = new();
    private string _lastTitle = string.Empty;
    private string _lastArtist = string.Empty;
    private string[] _plainLyricLines = Array.Empty<string>();
    private TimeSpan _songDuration = TimeSpan.Zero;
    private int _fetching; // 0 = idle, 1 = fetching
    private LyricLanguage _preferredLanguage = LyricLanguage.Simplified;

    // Cache original (Traditional) lyrics for language switching
    private List<LyricLine> _parsedLyricsTraditional = new();
    private string[] _plainLyricLinesTraditional = Array.Empty<string>();

    public bool HasLyrics => _parsedLyrics.Count > 0 || _plainLyricLines.Length > 0;

    /// <summary>
    /// The real song duration (from lyrics API), may differ from SMTC-reported duration.
    /// </summary>
    public TimeSpan RealDuration => _songDuration;

    public LyricLanguage PreferredLanguage
    {
        get => _preferredLanguage;
        set
        {
            if (_preferredLanguage != value)
            {
                _preferredLanguage = value;
                ApplyLanguagePreference();
            }
        }
    }

    /// <summary>
    /// Fetch lyrics for a song. Caches by title+artist, only re-fetches when song changes.
    /// Tries LRCLIB first, then falls back to NetEase Cloud Music API.
    /// </summary>
    public async Task<bool> EnsureLyricsAsync(string title, string artist, TimeSpan duration, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        // Already have lyrics for this song
        if (title == _lastTitle && artist == _lastArtist && HasLyrics)
            return true;

        // Already fetching (atomic compare)
        if (Interlocked.CompareExchange(ref _fetching, 1, 0) != 0)
            return false;

        _lastTitle = title;
        _lastArtist = artist;
        _songDuration = duration;

        // Temp variables: only replace old lyrics on success
        List<LyricLine>? tempParsed = null;
        string[]? tempPlain = null;
        TimeSpan tempDuration = duration;

        try
        {
            // Phase 1: Try NetEase first to get real duration and lyrics
            var (netEaseFound, realDuration, netEaseParsed) = await SearchNetEaseAsync(title, artist, ct);

            if (netEaseFound && netEaseParsed != null && netEaseParsed.Count > 0)
            {
                tempParsed = netEaseParsed;
                if (realDuration > TimeSpan.Zero)
                    tempDuration = realDuration;
            }

            // Phase 2: Try LRCLIB with the real duration for better matching
            if (tempParsed == null)
            {
                var effectiveDuration = tempDuration;
                var (lrclibParsed, lrclibPlain) = await SearchLrclibAsync(title, artist, effectiveDuration, ct);
                if (lrclibParsed != null && lrclibParsed.Count > 0)
                    tempParsed = lrclibParsed;
                else if (lrclibPlain != null && lrclibPlain.Length > 0)
                    tempPlain = lrclibPlain;
            }

            // Apply results: only replace if we found something new
            _songDuration = tempDuration;

            if (tempParsed != null && tempParsed.Count > 0)
            {
                _parsedLyricsTraditional = tempParsed;
            }
            else if (tempPlain != null && tempPlain.Length > 0)
            {
                _plainLyricLines = tempPlain;
                _plainLyricLinesTraditional = (string[])tempPlain.Clone();
            }

            // Apply language preference (convert to Simplified if needed)
            ApplyLanguagePreference();

            return HasLyrics;
        }
        catch
        {
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _fetching, 0);
        }
    }

    /// <summary>
    /// Search LRCLIB for lyrics. Returns parsed lines and/or plain text.
    /// </summary>
    private async Task<(List<LyricLine>? Parsed, string[]? Plain)> SearchLrclibAsync(string title, string artist, TimeSpan duration, CancellationToken ct)
    {
        var cleanedTitle = CleanTitle(title);
        // Build search titles: try cleaned first, then original if different and non-empty
        var searchTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(cleanedTitle) && cleanedTitle != title)
            searchTitles.Add(cleanedTitle);
        if (!string.IsNullOrWhiteSpace(title))
            searchTitles.Add(title);
        if (searchTitles.Count == 0)
            return (null, null);

        string? bestSyncedLrc = null;
        double bestSyncedDiff = double.MaxValue;
        string? bestPlain = null;
        double bestPlainDiff = double.MaxValue;

        foreach (var searchTitle in searchTitles)
        {
            // Search with artist first, then without artist (broader results)
            var urlWithArtist = !string.IsNullOrWhiteSpace(artist)
                ? $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(searchTitle)}&artist_name={Uri.EscapeDataString(artist)}"
                : null;
            var urlNoArtist = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(searchTitle)}";

            var searchUrls = urlWithArtist != null
                ? new[] { (urlWithArtist, true), (urlNoArtist, false) }
                : new[] { (urlNoArtist, false) };

            foreach (var (url, withArtist) in searchUrls)
            {
                for (int attempt = 0; attempt <= MaxRetries; attempt++)
                {
                try
                {
                    var response = await HttpClient.GetAsync(url, ct);
                    if (!response.IsSuccessStatusCode)
                        break;

                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);

                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var dur = item.TryGetProperty("duration", out var durProp) ? durProp.GetDouble() : 0;
                        var diff = duration.TotalSeconds > 0 ? Math.Abs(dur - duration.TotalSeconds) : 0;

                        if (item.TryGetProperty("syncedLyrics", out var syncedProp))
                        {
                            var synced = syncedProp.GetString();
                            if (!string.IsNullOrWhiteSpace(synced) && (bestSyncedLrc == null || diff < bestSyncedDiff))
                            {
                                bestSyncedLrc = synced;
                                bestSyncedDiff = diff;
                            }
                        }

                        if (bestSyncedLrc == null && item.TryGetProperty("plainLyrics", out var plainProp))
                        {
                            var plain = plainProp.GetString();
                            if (!string.IsNullOrWhiteSpace(plain) && (bestPlain == null || diff < bestPlainDiff))
                            {
                                bestPlain = plain;
                                bestPlainDiff = diff;
                            }
                        }
                    }
                }
                catch
                {
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(1000 * (attempt + 1), ct);
                        continue;
                    }
                }
                break; // success or exhausted retries
                } // for attempt
            }

            if (bestSyncedLrc != null)
                break;
        }

        // Return results
        if (!string.IsNullOrWhiteSpace(bestSyncedLrc))
        {
            var parsed = ParseLrcLines(bestSyncedLrc);
            if (parsed.Count > 0)
                return (parsed, null);
        }

        if (!string.IsNullOrWhiteSpace(bestPlain))
        {
            var plain = bestPlain.Split('\n', '\r')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToArray();
            if (plain.Length > 0)
                return (null, plain);
        }

        return (null, null);
    }

    private async Task<(bool Found, TimeSpan RealDuration, List<LyricLine>? Parsed)> SearchNetEaseAsync(string title, string artist, CancellationToken ct)
    {
        try
        {
            var cleanedTitle = CleanTitle(title);
            var searchTitle = !string.IsNullOrWhiteSpace(cleanedTitle) ? cleanedTitle : title;

            // Multi-keyword fallback: same strategy as LRCLIB
            var keywords = new List<string>();
            if (!string.IsNullOrWhiteSpace(searchTitle) && !string.IsNullOrWhiteSpace(artist))
                keywords.Add($"{searchTitle} {artist}");
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
                keywords.Add($"{title} {artist}");
            if (!string.IsNullOrWhiteSpace(searchTitle))
                keywords.Add(searchTitle);
            if (!string.IsNullOrWhiteSpace(title))
                keywords.Add(title);

            var searchHeaders = new Dictionary<string, string>
            {
                ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ["Referer"] = "https://music.163.com"
            };

            foreach (var keyword in keywords.Distinct())
            {
                var result = await SearchNetEaseSingleAsync(keyword, title, artist, _songDuration, searchHeaders, ct);
                if (result.Found)
                    return result;
            }

            return (false, TimeSpan.Zero, null);
        }
        catch
        {
            return (false, TimeSpan.Zero, null);
        }
    }

    private async Task<(bool Found, TimeSpan RealDuration, List<LyricLine>? Parsed)> SearchNetEaseSingleAsync(
        string keyword, string originalTitle, string artist, TimeSpan duration,
        Dictionary<string, string> headers, CancellationToken ct)
    {
        var searchUrl = "https://music.163.com/api/search/get";
        var searchBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["s"] = keyword,
            ["type"] = "1",
            ["limit"] = "20",
            ["offset"] = "0"
        });

        var request = new HttpRequestMessage(HttpMethod.Post, searchUrl) { Content = searchBody };
        foreach (var h in headers)
            request.Headers.TryAddWithoutValidation(h.Key, h.Value);

        var searchResponse = await HttpClient.SendAsync(request, ct);
        if (!searchResponse.IsSuccessStatusCode)
            return (false, TimeSpan.Zero, null);

        var searchJson = await searchResponse.Content.ReadAsStringAsync(ct);
        using var searchDoc = JsonDocument.Parse(searchJson);

        if (!searchDoc.RootElement.TryGetProperty("result", out var result) ||
            !result.TryGetProperty("songs", out var songs) ||
            songs.GetArrayLength() == 0)
        {
            return (false, TimeSpan.Zero, null);
        }

        // Score and rank all candidates
        var candidates = new List<(long Id, double Score, TimeSpan Duration)>();
        foreach (var song in songs.EnumerateArray())
        {
            var songId = song.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
            var durationMs = song.TryGetProperty("duration", out var durProp) ? durProp.GetInt64() : 0;
            var songDuration = durationMs > 0 ? TimeSpan.FromMilliseconds(durationMs) : TimeSpan.Zero;
            if (songId == 0) continue;

            var songName = song.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            var songArtist = "";
            if (song.TryGetProperty("artists", out var artistsProp) && artistsProp.GetArrayLength() > 0)
                songArtist = artistsProp[0].TryGetProperty("name", out var anProp) ? anProp.GetString() ?? "" : "";

            var score = ScoreNetEaseResult(songName, songArtist, songDuration, originalTitle, artist, duration);
            candidates.Add((songId, score, songDuration));
        }

        // Sort by score descending, take top results
        candidates = candidates.OrderByDescending(c => c.Score).ToList();

        foreach (var (songId, score, songDuration) in candidates)
        {
            // Reject low-score results
            if (score < 80) continue;

            var lyricUrl = $"https://music.163.com/api/lyric?id={songId}";
            var lyricRequest = new HttpRequestMessage(HttpMethod.Get, lyricUrl);
            foreach (var h in headers)
                lyricRequest.Headers.TryAddWithoutValidation(h.Key, h.Value);

            var lyricResponse = await HttpClient.SendAsync(lyricRequest, ct);
            if (!lyricResponse.IsSuccessStatusCode) continue;

            var lyricJson = await lyricResponse.Content.ReadAsStringAsync(ct);
            using var lyricDoc = JsonDocument.Parse(lyricJson);

            if (!lyricDoc.RootElement.TryGetProperty("lrc", out var lrcObj) ||
                !lrcObj.TryGetProperty("lyric", out var lyricProp))
                continue;

            var lrcText = lyricProp.GetString();
            if (string.IsNullOrWhiteSpace(lrcText)) continue;

            var tempParsed = ParseLrcLines(lrcText);
            if (tempParsed.Count > 0)
            {
                return (true, songDuration, tempParsed);
            }
        }

        return (false, TimeSpan.Zero, null);
    }

    private static double ScoreNetEaseResult(string songName, string songArtist, TimeSpan songDuration,
        string originalTitle, string artist, TimeSpan duration)
    {
        double score = 0;

        // Title similarity (max 100)
        var titleSim = StringSimilarity(songName, originalTitle);
        score += titleSim * 100;

        // Artist similarity (max 80)
        if (!string.IsNullOrWhiteSpace(artist))
        {
            var artistSim = StringSimilarity(songArtist, artist);
            score += artistSim * 80;
        }
        else
        {
            score += 40; // No artist to compare, give partial credit
        }

        // Duration match (max 60)
        if (duration.TotalSeconds > 0 && songDuration.TotalSeconds > 0)
        {
            var diff = Math.Abs(songDuration.TotalSeconds - duration.TotalSeconds);
            var ratio = diff / duration.TotalSeconds;
            if (ratio < 0.05) score += 60;       // Within 5%: perfect
            else if (ratio < 0.10) score += 45;  // Within 10%
            else if (ratio < 0.20) score += 25;  // Within 20%
            else if (ratio < 0.30) score += 10;  // Within 30%
            // Over 30%: no duration score
        }
        else
        {
            score += 30; // Duration unknown, give partial credit
        }

        return score;
    }

    private static double StringSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0;

        a = a.ToLowerInvariant().Trim();
        b = b.ToLowerInvariant().Trim();

        if (a == b) return 1.0;
        if (a.Contains(b) || b.Contains(a)) return 0.8;

        // Simple word overlap ratio
        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (wordsA.Length == 0 || wordsB.Length == 0) return 0;

        var overlap = wordsA.Intersect(wordsB).Count();
        return (double)overlap / Math.Max(wordsA.Length, wordsB.Length) * 0.7;
    }

    private static List<LyricLine> ParseLrcLines(string lrcText)
    {
        var result = new List<LyricLine>();
        foreach (var line in lrcText.Split('\n', '\r'))
        {
            var match = LrcRegex.Match(line.Trim());
            if (match.Success)
            {
                var min = int.Parse(match.Groups[1].Value);
                var sec = int.Parse(match.Groups[2].Value);
                var ms = int.Parse(match.Groups[3].Value.PadRight(3, '0'));
                var text = match.Groups[4].Value.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(new LyricLine(new TimeSpan(0, 0, min, sec, ms), text));
            }
        }
        return result;
    }

    private void ParseLrc(string lrcText)
    {
        _parsedLyrics = ParseLrcLines(lrcText);
    }

    /// <summary>
    /// Get the lyric line for the current playback position.
    /// When position is 0 (e.g. SMTC not reporting timeline), returns first lyric.
    /// </summary>
    public string? GetCurrentLyric(TimeSpan position)
    {
        if (_parsedLyrics.Count > 0)
        {
            // When position is 0 or before first lyric, show first line
            if (position <= TimeSpan.Zero)
                return _parsedLyrics[0].Text;

            LyricLine? current = null;
            foreach (var line in _parsedLyrics)
            {
                if (line.Time <= position)
                    current = line;
                else
                    break;
            }
            return current?.Text ?? _parsedLyrics[0].Text;
        }

        // For plain lyrics, show first line when duration unknown, else estimate by ratio
        if (_plainLyricLines.Length > 0)
        {
            if (_songDuration.TotalSeconds <= 0 || position <= TimeSpan.Zero)
                return _plainLyricLines[0];

            var ratio = Math.Clamp(position.TotalSeconds / _songDuration.TotalSeconds, 0, 0.99);
            var index = (int)(ratio * _plainLyricLines.Length);
            if (index >= _plainLyricLines.Length)
                index = _plainLyricLines.Length - 1;
            return _plainLyricLines[index];
        }

        return null;
    }

    public IReadOnlyList<string> GetCurrentLyricSequence(TimeSpan position, int maxLines)
    {
        if (maxLines <= 0)
        {
            return Array.Empty<string>();
        }

        if (_parsedLyrics.Count > 0)
        {
            var index = GetCurrentParsedLyricIndex(position);
            return _parsedLyrics
                .Skip(index)
                .Take(maxLines)
                .Select(line => line.Text)
                .ToArray();
        }

        if (_plainLyricLines.Length > 0)
        {
            var index = GetCurrentPlainLyricIndex(position);
            return _plainLyricLines
                .Skip(index)
                .Take(maxLines)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    public TimeSpan GetCurrentLyricDuration(TimeSpan position)
    {
        if (_parsedLyrics.Count < 2)
        {
            return TimeSpan.Zero;
        }

        var index = GetCurrentParsedLyricIndex(position);
        if (index < 0 || index >= _parsedLyrics.Count - 1)
        {
            return TimeSpan.Zero;
        }

        return _parsedLyrics[index + 1].Time - _parsedLyrics[index].Time;
    }

    private int GetCurrentParsedLyricIndex(TimeSpan position)
    {
        if (_parsedLyrics.Count == 0 || position <= TimeSpan.Zero)
        {
            return 0;
        }

        var index = 0;
        for (var i = 0; i < _parsedLyrics.Count; i++)
        {
            if (_parsedLyrics[i].Time <= position)
            {
                index = i;
                continue;
            }

            break;
        }

        return index;
    }

    private int GetCurrentPlainLyricIndex(TimeSpan position)
    {
        if (_plainLyricLines.Length == 0 || _songDuration.TotalSeconds <= 0 || position <= TimeSpan.Zero)
        {
            return 0;
        }

        var ratio = Math.Clamp(position.TotalSeconds / _songDuration.TotalSeconds, 0, 0.99);
        return Math.Min((int)(ratio * _plainLyricLines.Length), _plainLyricLines.Length - 1);
    }

    private static string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        // Remove Chinese and English parenthetical suffixes at the end
        var cleaned = Regex.Replace(title, @"\s*[（(].*?[)）]\s*$", string.Empty);
        // Remove square bracket tags at the end: [Live], 【吉他版】
        cleaned = Regex.Replace(cleaned, @"\s*[\[【].*?[】\]]\s*$", string.Empty);
        // Remove common music symbols that cause search issues: ★ ☆ ♪ ♫ ♬ ♩ ♫ ♬
        cleaned = Regex.Replace(cleaned, @"[★☆♪♫♬♩♪♫♬✦✧✨💫⭐🌟]", string.Empty);
        // Remove leading/trailing dots and dashes
        cleaned = Regex.Replace(cleaned, @"^[\.\-—\s]+|[\.\-—\s]+$", string.Empty);
        return cleaned.Trim();
    }

    public void Clear()
    {
        _parsedLyrics.Clear();
        _parsedLyricsTraditional.Clear();
        _plainLyricLines = Array.Empty<string>();
        _plainLyricLinesTraditional = Array.Empty<string>();
        _lastTitle = string.Empty;
        _lastArtist = string.Empty;
        _songDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Apply language preference by converting lyrics to the desired script.
    /// </summary>
    private void ApplyLanguagePreference()
    {
        if (_preferredLanguage == LyricLanguage.Traditional)
        {
            _parsedLyrics = new List<LyricLine>(_parsedLyricsTraditional);
            _plainLyricLines = (string[])_plainLyricLinesTraditional.Clone();
            return;
        }

        // Convert to Simplified Chinese
        _parsedLyrics = _parsedLyricsTraditional
            .Select(l => new LyricLine(l.Time, ChineseConverter.ToSimplified(l.Text)))
            .ToList();

        _plainLyricLines = _plainLyricLinesTraditional
            .Select(l => ChineseConverter.ToSimplified(l))
            .ToArray();
    }
}
