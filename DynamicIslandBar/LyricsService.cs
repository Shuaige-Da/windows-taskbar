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
        _parsedLyrics.Clear();
        _plainLyricLines = Array.Empty<string>();

        try
        {
            // Phase 1: Try NetEase first to get real duration and lyrics
            var (netEaseFound, realDuration) = await SearchNetEaseAsync(title, artist, ct);

            // Update duration with the real one from NetEase (SMTC often reports 0)
            if (realDuration > TimeSpan.Zero)
                _songDuration = realDuration;

            // Phase 2: Try LRCLIB with the real duration for better matching
            if (!netEaseFound)
            {
                await SearchLrclibAsync(title, artist, _songDuration, ct);
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
    /// Search LRCLIB for lyrics. Returns true if synced or plain lyrics were found.
    /// </summary>
    private async Task<bool> SearchLrclibAsync(string title, string artist, TimeSpan duration, CancellationToken ct)
    {
        var cleanedTitle = CleanTitle(title);
        // Build search titles: try cleaned first, then original if different and non-empty
        var searchTitles = new List<string>();
        if (!string.IsNullOrWhiteSpace(cleanedTitle) && cleanedTitle != title)
            searchTitles.Add(cleanedTitle);
        if (!string.IsNullOrWhiteSpace(title))
            searchTitles.Add(title);
        if (searchTitles.Count == 0)
            return false;

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

        // Store results
        _parsedLyricsTraditional.Clear();
        _plainLyricLinesTraditional = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(bestSyncedLrc))
        {
            ParseLrc(bestSyncedLrc);
            _parsedLyricsTraditional = new List<LyricLine>(_parsedLyrics);
        }

        if (_parsedLyrics.Count == 0 && !string.IsNullOrWhiteSpace(bestPlain))
        {
            _plainLyricLines = bestPlain.Split('\n', '\r')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToArray();
            _plainLyricLinesTraditional = (string[])_plainLyricLines.Clone();
        }

        return HasLyrics;
    }

    private async Task<(bool Found, TimeSpan RealDuration)> SearchNetEaseAsync(string title, string artist, CancellationToken ct)
    {
        try
        {
            var cleanedTitle = CleanTitle(title);
            var searchTitle = !string.IsNullOrWhiteSpace(cleanedTitle) ? cleanedTitle : title;
            var keyword = !string.IsNullOrWhiteSpace(artist)
                ? $"{searchTitle} {artist}"
                : searchTitle;

            var searchUrl = "https://music.163.com/api/search/get";
            var searchBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["s"] = keyword,
                ["type"] = "1",
                ["limit"] = "5",
                ["offset"] = "0"
            });

            var searchResponse = await HttpClient.PostAsync(searchUrl, searchBody, ct);
            if (!searchResponse.IsSuccessStatusCode)
                return (false, TimeSpan.Zero);

            var searchJson = await searchResponse.Content.ReadAsStringAsync(ct);
            using var searchDoc = JsonDocument.Parse(searchJson);

            if (!searchDoc.RootElement.TryGetProperty("result", out var result) ||
                !result.TryGetProperty("songs", out var songs) ||
                songs.GetArrayLength() == 0)
            {
                return (false, TimeSpan.Zero);
            }

            foreach (var song in songs.EnumerateArray())
            {
                var songId = song.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
                var durationMs = song.TryGetProperty("duration", out var durProp) ? durProp.GetInt64() : 0;
                var realDuration = durationMs > 0 ? TimeSpan.FromMilliseconds(durationMs) : TimeSpan.Zero;
                if (songId == 0) continue;

                var lyricUrl = $"https://music.163.com/api/lyric?id={songId}";
                var lyricResponse = await HttpClient.GetAsync(lyricUrl, ct);
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
                    _parsedLyricsTraditional = tempParsed;
                    return (true, realDuration);
                }
            }

            return (false, TimeSpan.Zero);
        }
        catch
        {
            return (false, TimeSpan.Zero);
        }
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
