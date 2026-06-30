using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace DynamicIslandBar;

public record LyricLine(TimeSpan Time, string Text);

/// <summary>
/// Fetches time-synced lyrics from LRCLIB (https://lrclib.net) free API.
/// Parses LRC format and provides current lyric line by playback position.
/// Prefers synced lyrics (with timestamps) over plain lyrics.
/// </summary>
public class LyricsService
{
    private static readonly HttpClient HttpClient = new();
    private List<LyricLine> _parsedLyrics = new();
    private string _lastTitle = string.Empty;
    private string _lastArtist = string.Empty;
    private string[] _plainLyricLines = Array.Empty<string>();
    private TimeSpan _songDuration = TimeSpan.Zero;
    private bool _fetching;

    public bool HasLyrics => _parsedLyrics.Count > 0 || _plainLyricLines.Length > 0;

    /// <summary>
    /// Fetch lyrics for a song. Caches by title+artist, only re-fetches when song changes.
    /// Tries multiple search strategies and prefers results with synced (timestamped) lyrics.
    /// </summary>
    public async Task<bool> EnsureLyricsAsync(string title, string artist, TimeSpan duration, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        // Already have lyrics for this song
        if (title == _lastTitle && artist == _lastArtist && HasLyrics)
            return true;

        // Already fetching
        if (_fetching)
            return false;

        _fetching = true;
        _lastTitle = title;
        _lastArtist = artist;
        _songDuration = duration;
        _parsedLyrics.Clear();
        _plainLyricLines = Array.Empty<string>();

        try
        {
            var cleanedTitle = CleanTitle(title);
            var searchTitles = cleanedTitle != title
                ? new[] { cleanedTitle, title }
                : new[] { title };

            // Collect the best synced and plain lyrics across all searches
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
                    System.Diagnostics.Debug.WriteLine($"[Lyrics] GET {url}");
                    var response = await HttpClient.GetAsync(url, ct);
                    System.Diagnostics.Debug.WriteLine($"[Lyrics] Response: {(int)response.StatusCode} {response.StatusCode}");
                    if (!response.IsSuccessStatusCode)
                        continue;

                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);

                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var dur = item.TryGetProperty("duration", out var durProp) ? durProp.GetDouble() : 0;
                        var diff = duration.TotalSeconds > 0 ? Math.Abs(dur - duration.TotalSeconds) : 0;

                        // Check for synced lyrics
                        if (item.TryGetProperty("syncedLyrics", out var syncedProp))
                        {
                            var synced = syncedProp.GetString();
                            if (!string.IsNullOrWhiteSpace(synced))
                            {
                                if (bestSyncedLrc == null || diff < bestSyncedDiff)
                                {
                                    bestSyncedLrc = synced;
                                    bestSyncedDiff = diff;
                                    System.Diagnostics.Debug.WriteLine($"[Lyrics] Found synced (diff={diff:F1}s, withArtist={withArtist})");
                                }
                            }
                        }

                        // Also track best plain lyrics as fallback
                        if (bestSyncedLrc == null && item.TryGetProperty("plainLyrics", out var plainProp))
                        {
                            var plain = plainProp.GetString();
                            if (!string.IsNullOrWhiteSpace(plain))
                            {
                                if (bestPlain == null || diff < bestPlainDiff)
                                {
                                    bestPlain = plain;
                                    bestPlainDiff = diff;
                                    System.Diagnostics.Debug.WriteLine($"[Lyrics] Found plain (diff={diff:F1}s, withArtist={withArtist})");
                                }
                            }
                        }
                    }
                }

                // If we found synced lyrics, no need to try more search titles
                if (bestSyncedLrc != null)
                    break;
            }

            // Parse synced lyrics if available
            if (!string.IsNullOrWhiteSpace(bestSyncedLrc))
            {
                ParseLrc(bestSyncedLrc);
                System.Diagnostics.Debug.WriteLine($"[Lyrics] Parsed {(_parsedLyrics.Count)} synced lyric lines");
            }

            // Fallback to plain lyrics
            if (_parsedLyrics.Count == 0 && !string.IsNullOrWhiteSpace(bestPlain))
            {
                _plainLyricLines = bestPlain.Split('\n', '\r')
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToArray();
                System.Diagnostics.Debug.WriteLine($"[Lyrics] Using {(_plainLyricLines.Length)} plain lyric lines (no synced available)");
            }

            _fetching = false;
            return HasLyrics;
        }
        catch
        {
            _fetching = false;
            return false;
        }
    }

    /// <summary>
    /// Parse LRC format: [mm:ss.xx]lyric text
    /// </summary>
    private void ParseLrc(string lrcText)
    {
        _parsedLyrics.Clear();
        var regex = new Regex(@"\[(\d{1,2}):(\d{2})[.:](\d{1,3})\](.*)");

        foreach (var line in lrcText.Split('\n', '\r'))
        {
            var match = regex.Match(line.Trim());
            if (match.Success)
            {
                var min = int.Parse(match.Groups[1].Value);
                var sec = int.Parse(match.Groups[2].Value);
                var msStr = match.Groups[3].Value.PadRight(3, '0');
                var ms = int.Parse(msStr);
                var time = new TimeSpan(0, 0, min, sec, ms);
                var text = match.Groups[4].Value.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _parsedLyrics.Add(new LyricLine(time, text));
                }
            }
        }
    }

    /// <summary>
    /// Get the lyric line for the current playback position.
    /// For synced lyrics: returns the line at the current timestamp.
    /// For plain lyrics: estimates the current line based on position/duration ratio.
    /// Returns null if no lyrics available.
    /// </summary>
    public string? GetCurrentLyric(TimeSpan position)
    {
        if (_parsedLyrics.Count > 0)
        {
            LyricLine? current = null;
            foreach (var line in _parsedLyrics)
            {
                if (line.Time <= position)
                    current = line;
                else
                    break;
            }
            return current?.Text;
        }

        // For plain lyrics, estimate current line based on position/duration ratio
        if (_plainLyricLines.Length > 0 && _songDuration.TotalSeconds > 0)
        {
            var ratio = Math.Clamp(position.TotalSeconds / _songDuration.TotalSeconds, 0, 0.99);
            var index = (int)(ratio * _plainLyricLines.Length);
            if (index >= _plainLyricLines.Length)
                index = _plainLyricLines.Length - 1;
            return _plainLyricLines[index];
        }

        return null;
    }

    /// <summary>
    /// Get the first lyric line (for initial display).
    /// </summary>
    public string? GetFirstLyric()
    {
        if (_parsedLyrics.Count > 0)
            return _parsedLyrics[0].Text;
        return _plainLyricLines.Length > 0 ? _plainLyricLines[0] : null;
    }

    /// <summary>
    /// Clean song title by removing version suffixes for better LRCLIB matching.
    /// "三号线（吉他版）" → "三号线", "Song (Live)" → "Song"
    /// </summary>
    private static string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return title;

        // Remove Chinese and English parenthetical suffixes
        var cleaned = Regex.Replace(title, @"\s*[（(].*?[)）]\s*$", string.Empty);
        return cleaned.Trim();
    }

    public void Clear()
    {
        _parsedLyrics.Clear();
        _plainLyricLines = Array.Empty<string>();
        _lastTitle = string.Empty;
        _lastArtist = string.Empty;
        _songDuration = TimeSpan.Zero;
    }
}
