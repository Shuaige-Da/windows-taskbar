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
    private TimeSpan _lastRequestedDuration = TimeSpan.Zero;
    private LyricLanguage _preferredLanguage = LyricLanguage.Simplified;
    private readonly LyricsFetchCoordinator _fetchCoordinator = new();
    private Task<LyricFetchPayload?>? _activeFetchTask;
    private string _activeFetchSongKey = string.Empty;
    private LyricsFetchToken? _activeFetchToken;
    private readonly Dictionary<string, LyricFetchPayload> _payloadCache = new(StringComparer.Ordinal);
    private readonly Queue<string> _payloadCacheOrder = new();
    private const int PayloadCacheCapacity = 24;
    private const int MaximumNetEaseLyricCandidates = 6;

    // Cache original (Traditional) lyrics for language switching
    private List<LyricLine> _parsedLyricsTraditional = new();
    private string[] _plainLyricLinesTraditional = Array.Empty<string>();

    public bool HasLyrics => _parsedLyrics.Count > 0 || _plainLyricLines.Length > 0;

    /// <summary>
    /// The real song duration (from lyrics API), may differ from SMTC-reported duration.
    /// </summary>
    public TimeSpan RealDuration => _songDuration;

    public bool HasLyricsFor(string? title, string? artist, TimeSpan? duration = null)
    {
        if (!HasLyrics
            || !string.Equals(NormalizeIdentityPart(title), NormalizeIdentityPart(_lastTitle), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(NormalizeIdentityPart(artist), NormalizeIdentityPart(_lastArtist), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (duration is not { } expectedDuration
            || expectedDuration <= TimeSpan.Zero
            || _lastRequestedDuration <= TimeSpan.Zero)
        {
            return true;
        }

        var toleranceSeconds = Math.Max(15d, expectedDuration.TotalSeconds * 0.05d);
        return Math.Abs((_lastRequestedDuration - expectedDuration).TotalSeconds) <= toleranceSeconds;
    }

    private sealed record LyricFetchPayload(
        List<LyricLine> SyncedLyricsTraditional,
        string[] PlainLyricsTraditional,
        TimeSpan RealDuration);

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
    /// Tries NetEase first, then falls back to LRCLIB.
    /// </summary>
    public async Task<bool> EnsureLyricsAsync(string title, string artist, TimeSpan duration, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (HasLyricsFor(title, artist, duration))
        {
            return true;
        }

        var songKey = BuildSongKey(title, artist, duration);
        if (_payloadCache.TryGetValue(songKey, out var cachedPayload))
        {
            ApplyPayload(title, artist, duration, cachedPayload);
            ApplyLanguagePreference();
            return true;
        }

        Task<LyricFetchPayload?> fetchTask;
        LyricsFetchToken token;
        if (_activeFetchTask is not null
            && _activeFetchToken is { } existingToken
            && string.Equals(songKey, _activeFetchSongKey, StringComparison.Ordinal))
        {
            fetchTask = _activeFetchTask;
            token = existingToken;
        }
        else
        {
            token = _fetchCoordinator.Begin(songKey);
            _activeFetchSongKey = songKey;
            _activeFetchToken = token;
            _activeFetchTask = FetchLyricsPayloadAsync(title, artist, duration, ct);
            fetchTask = _activeFetchTask;
        }

        try
        {
            var payload = await fetchTask;

            if (payload is null || !_fetchCoordinator.IsCurrent(token))
            {
                return false;
            }

            ApplyPayload(title, artist, duration, payload);
            ApplyLanguagePreference();
            CachePayload(songKey, payload);
            return HasLyricsFor(title, artist, duration);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (ReferenceEquals(_activeFetchTask, fetchTask) && _fetchCoordinator.IsCurrent(token))
            {
                _activeFetchTask = null;
                _activeFetchSongKey = string.Empty;
                _activeFetchToken = null;
            }
        }
    }

    private async Task<LyricFetchPayload?> FetchLyricsPayloadAsync(
        string title,
        string artist,
        TimeSpan duration,
        CancellationToken ct)
    {
        var identity = LyricSearchMetadataPolicy.BuildIdentity(title, artist, duration);
        var netEasePayload = await SearchNetEaseAsync(identity, ct);
        if (netEasePayload is not null)
        {
            return netEasePayload;
        }

        return await SearchLrclibAsync(identity, ct);
    }

    private void CachePayload(string songKey, LyricFetchPayload payload)
    {
        if (_payloadCache.ContainsKey(songKey))
        {
            _payloadCache[songKey] = payload;
            return;
        }

        _payloadCache[songKey] = payload;
        _payloadCacheOrder.Enqueue(songKey);
        while (_payloadCacheOrder.Count > PayloadCacheCapacity)
        {
            _payloadCache.Remove(_payloadCacheOrder.Dequeue());
        }
    }

    private void ApplyPayload(
        string title,
        string artist,
        TimeSpan fallbackDuration,
        LyricFetchPayload payload)
    {
        _lastTitle = title;
        _lastArtist = artist;
        _lastRequestedDuration = fallbackDuration;
        _songDuration = payload.RealDuration > TimeSpan.Zero ? payload.RealDuration : fallbackDuration;
        _parsedLyricsTraditional = payload.SyncedLyricsTraditional;
        _plainLyricLinesTraditional = payload.PlainLyricsTraditional;
        _parsedLyrics = new List<LyricLine>(_parsedLyricsTraditional);
        _plainLyricLines = (string[])_plainLyricLinesTraditional.Clone();
    }

    private static string BuildSongKey(string title, string artist, TimeSpan duration)
    {
        var identity = LyricSearchMetadataPolicy.BuildIdentity(title, artist, duration);
        var durationSeconds = duration > TimeSpan.Zero ? Math.Round(duration.TotalSeconds) : 0d;
        return $"{NormalizeIdentityPart(identity.Title)}\u001f{NormalizeIdentityPart(identity.Artist)}\u001f{durationSeconds:0}";
    }

    private static string NormalizeIdentityPart(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    /// <summary>
    /// Search LRCLIB for lyrics. Returns synced lyrics first, plain lyrics as fallback.
    /// </summary>
    private async Task<LyricFetchPayload?> SearchLrclibAsync(LyricSearchIdentity identity, CancellationToken ct)
    {
        var searchQueries = LyricSearchMetadataPolicy.BuildQueries(identity);
        if (searchQueries.Count == 0)
        {
            return null;
        }

        LyricFetchPayload? bestPayload = null;
        double bestScore = 0d;

        foreach (var query in searchQueries)
        {
            var urlWithArtist = !string.IsNullOrWhiteSpace(query.Artist)
                ? $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(query.Title)}&artist_name={Uri.EscapeDataString(query.Artist)}"
                : null;
            var urlNoArtist = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(query.Title)}";

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
                        {
                            break;
                        }

                        var json = await response.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);

                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            var syncedLyrics = GetStringProperty(item, "syncedLyrics");
                            var plainLyrics = GetStringProperty(item, "plainLyrics");
                            var duration = item.TryGetProperty("duration", out var durProp)
                                ? TimeSpan.FromSeconds(durProp.GetDouble())
                                : TimeSpan.Zero;
                            var candidate = new LyricCandidate(
                                "lrclib",
                                GetStringProperty(item, "id"),
                                GetStringProperty(item, "trackName"),
                                GetStringProperty(item, "artistName"),
                                duration,
                                !string.IsNullOrWhiteSpace(syncedLyrics),
                                !string.IsNullOrWhiteSpace(plainLyrics),
                                false,
                                false);

                            var score = LyricMatchingPolicy.Score(identity, candidate);
                            if (score < LyricMatchingPolicy.MinimumAcceptedScore || score <= bestScore)
                            {
                                continue;
                            }

                            var payload = candidate.HasSyncedLyrics
                                ? CreateSyncedPayload(syncedLyrics, duration)
                                : CreatePlainPayload(plainLyrics, duration);
                            if (payload is null)
                            {
                                continue;
                            }

                            bestScore = score;
                            bestPayload = payload;
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

                    break;
                } // for attempt
            }
        }

        return bestPayload;
    }

    private async Task<LyricFetchPayload?> SearchNetEaseAsync(LyricSearchIdentity identity, CancellationToken ct)
    {
        try
        {
            var candidates = new List<LyricCandidate>();
            var discoveredSongIds = new HashSet<string>(StringComparer.Ordinal);
            var searchUrl = "https://music.163.com/api/search/get";

            foreach (var query in LyricSearchMetadataPolicy.BuildQueries(identity))
            {
                var keyword = !string.IsNullOrWhiteSpace(query.Artist)
                    ? $"{query.Title} {query.Artist}"
                    : query.Title;
                var searchBody = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["s"] = keyword,
                    ["type"] = "1",
                    ["limit"] = "20",
                    ["offset"] = "0"
                });

                using var searchRequest = CreateNetEaseRequest(HttpMethod.Post, searchUrl, searchBody);
                using var searchResponse = await HttpClient.SendAsync(searchRequest, ct);
                if (!searchResponse.IsSuccessStatusCode)
                {
                    continue;
                }

                var searchJson = await searchResponse.Content.ReadAsStringAsync(ct);
                using var searchDoc = JsonDocument.Parse(searchJson);

                if (!searchDoc.RootElement.TryGetProperty("result", out var result) ||
                    !result.TryGetProperty("songs", out var songs) ||
                    songs.GetArrayLength() == 0)
                {
                    continue;
                }

                foreach (var song in songs.EnumerateArray())
                {
                    var songId = song.TryGetProperty("id", out var idProp) ? idProp.GetInt64() : 0;
                    var songIdText = songId.ToString();
                    var durationMs = song.TryGetProperty("duration", out var durProp) ? durProp.GetInt64() : 0;
                    var realDuration = durationMs > 0 ? TimeSpan.FromMilliseconds(durationMs) : TimeSpan.Zero;
                    if (songId == 0 || !discoveredSongIds.Add(songIdText))
                    {
                        continue;
                    }

                    candidates.Add(new LyricCandidate(
                        "netease",
                        songIdText,
                        GetStringProperty(song, "name"),
                        ExtractNetEaseArtist(song),
                        realDuration,
                        false,
                        false,
                        false,
                        false));
                }
            }

            var rankedCandidates = LyricMatchingPolicy.RankMetadataCandidates(
                identity,
                candidates,
                MaximumNetEaseLyricCandidates);
            foreach (var candidate in rankedCandidates)
            {
                try
                {
                    using var lyricDoc = await FetchNetEaseLyricDocumentAsync(candidate.Id, ct);
                    if (IsTruthyProperty(lyricDoc.RootElement, "nolyric")
                        || IsTruthyProperty(lyricDoc.RootElement, "uncollected"))
                    {
                        continue;
                    }

                    var lrcText = ExtractNetEaseLrcText(lyricDoc.RootElement);
                    var parsedLyrics = string.IsNullOrWhiteSpace(lrcText)
                        ? new List<LyricLine>()
                        : ParseLrcLines(lrcText);
                    if (parsedLyrics.Count > 0)
                    {
                        return new LyricFetchPayload(
                            parsedLyrics,
                            Array.Empty<string>(),
                            candidate.Duration);
                    }
                }
                catch when (!ct.IsCancellationRequested)
                {
                    // Try the next scored candidate when one lyric request is unavailable.
                }
            }

            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<JsonDocument> FetchNetEaseLyricDocumentAsync(string songId, CancellationToken ct)
    {
        var lyricUrl = $"https://music.163.com/api/lyric?id={songId}";
        using var lyricRequest = CreateNetEaseRequest(HttpMethod.Get, lyricUrl);
        using var lyricResponse = await HttpClient.SendAsync(lyricRequest, ct);
        lyricResponse.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await lyricResponse.Content.ReadAsStringAsync(ct));
    }

    private static HttpRequestMessage CreateNetEaseRequest(
        HttpMethod method,
        string url,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
        request.Headers.Referrer = new Uri("https://music.163.com/");
        return request;
    }

    private static LyricFetchPayload? CreateSyncedPayload(string lrcText, TimeSpan duration)
    {
        var parsedLyrics = ParseLrcLines(lrcText);
        return parsedLyrics.Count > 0
            ? new LyricFetchPayload(parsedLyrics, Array.Empty<string>(), duration)
            : null;
    }

    private static LyricFetchPayload? CreatePlainPayload(string plainLyrics, TimeSpan duration)
    {
        var lines = SplitPlainLyrics(plainLyrics);
        return lines.Length > 0
            ? new LyricFetchPayload(new List<LyricLine>(), lines, duration)
            : null;
    }

    private static string[] SplitPlainLyrics(string? plainLyrics)
    {
        return string.IsNullOrWhiteSpace(plainLyrics)
            ? Array.Empty<string>()
            : plainLyrics
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            _ => string.Empty
        };
    }

    private static string ExtractNetEaseArtist(JsonElement song)
    {
        if (song.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
        {
            return string.Join(", ", artists
                .EnumerateArray()
                .Select(artist => GetStringProperty(artist, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name)));
        }

        if (song.TryGetProperty("ar", out var ar) && ar.ValueKind == JsonValueKind.Array)
        {
            return string.Join(", ", ar
                .EnumerateArray()
                .Select(artist => GetStringProperty(artist, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name)));
        }

        return string.Empty;
    }

    private static string ExtractNetEaseLrcText(JsonElement root)
    {
        return root.TryGetProperty("lrc", out var lrcObj)
            && lrcObj.TryGetProperty("lyric", out var lyricProp)
            ? lyricProp.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool IsTruthyProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => property.TryGetInt32(out var value) && value != 0,
            JsonValueKind.String => bool.TryParse(property.GetString(), out var boolValue)
                ? boolValue
                : property.GetString() == "1",
            _ => false
        };
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

    public void Clear()
    {
        _fetchCoordinator.Invalidate();
        _activeFetchTask = null;
        _activeFetchSongKey = string.Empty;
        _activeFetchToken = null;
        _parsedLyrics.Clear();
        _parsedLyricsTraditional.Clear();
        _plainLyricLines = Array.Empty<string>();
        _plainLyricLinesTraditional = Array.Empty<string>();
        _lastTitle = string.Empty;
        _lastArtist = string.Empty;
        _lastRequestedDuration = TimeSpan.Zero;
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
