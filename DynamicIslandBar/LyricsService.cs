using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DynamicIslandBar;

public interface ILyricsProvider
{
    Task<string?> TryGetLrcAsync(
        string title,
        string artist,
        TimeSpan? duration,
        CancellationToken cancellationToken = default);
}

public sealed class LyricsService
{
    private readonly ILyricsProvider _provider;
    private readonly TimeSpan _negativeCacheDuration;
    private readonly int _loadRetryCount;
    private readonly TimeSpan _loadRetryDelay;
    private readonly Func<DateTime> _utcNow;
    private readonly ConcurrentDictionary<string, LyricsCacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task<LyricsCacheEntry>>> _inflightLoads = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PlaybackObservation> _observations = new(StringComparer.OrdinalIgnoreCase);

    public LyricsService(
        ILyricsProvider provider,
        TimeSpan? negativeCacheDuration = null,
        Func<DateTime>? utcNow = null,
        int loadRetryCount = 0,
        TimeSpan? loadRetryDelay = null)
    {
        _provider = provider;
        _negativeCacheDuration = negativeCacheDuration ?? TimeSpan.FromMinutes(2);
        _loadRetryCount = Math.Max(0, loadRetryCount);
        _loadRetryDelay = loadRetryDelay ?? TimeSpan.FromMilliseconds(300);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<CenterCardMediaSnapshot> ResolveCurrentLyricAsync(
        CenterCardMediaSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (!snapshot.IsMusicApp
            || string.IsNullOrWhiteSpace(snapshot.Title))
        {
            return snapshot;
        }

        var searchTitle = CleanTitle(snapshot.Title, snapshot.Artist);
        var cacheKey = BuildCacheKey(searchTitle, snapshot.Artist);
        var wasCacheHit = _cache.TryGetValue(cacheKey, out var entry);
        if (!wasCacheHit || entry == null || ShouldRefresh(entry))
        {
            var loadedEntry = await LoadEntryAsync(
                cacheKey,
                searchTitle,
                snapshot.Artist,
                snapshot.Duration,
                cancellationToken);
            entry = _cache.AddOrUpdate(
                cacheKey,
                loadedEntry,
                (_, existingEntry) => existingEntry.Lines.Count > 0 && loadedEntry.Lines.Count == 0
                    ? existingEntry
                    : loadedEntry);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var duration = snapshot.Duration ?? EstimateDuration(entry.Lines);
        var position = snapshot.Position ?? EstimatePosition(cacheKey, snapshot.IsPlaying, duration);
        var lyric = ResolveDisplayLyric(entry.Lines, position, searchTitle, snapshot.Artist);
        WriteDiagnostics(
            "resolve",
            searchTitle,
            snapshot.Artist,
            $"cacheHit={wasCacheHit}",
            $"lines={entry.Lines.Count}",
            $"position={position?.ToString() ?? "<null>"}",
            $"duration={duration?.ToString() ?? "<null>"}",
            $"lyric={lyric}");
        return snapshot with
        {
            Lyric = string.IsNullOrWhiteSpace(lyric) ? "暂无歌词" : lyric,
            Position = position,
            Duration = duration
        };
    }

    private async Task<LyricsCacheEntry> LoadEntryAsync(
        string cacheKey,
        string title,
        string artist,
        TimeSpan? duration,
        CancellationToken cancellationToken)
    {
        var lazy = _inflightLoads.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<LyricsCacheEntry>>(
                async () => new LyricsCacheEntry(
                    await LoadLinesAsync(title, artist, duration, cancellationToken),
                    _utcNow()),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value;
        }
        finally
        {
            _inflightLoads.TryRemove(cacheKey, out _);
        }
    }

    private async Task<IReadOnlyList<LrcLine>> LoadLinesAsync(
        string title,
        string artist,
        TimeSpan? duration,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _loadRetryCount; attempt++)
        {
            try
            {
                var lrc = await _provider.TryGetLrcAsync(title, artist, duration, cancellationToken);
                var lines = LrcParser.Parse(lrc);
                WriteDiagnostics(
                    "load",
                    title,
                    artist,
                    $"attempt={attempt}",
                    $"lrcLength={lrc?.Length ?? 0}",
                    $"lines={lines.Count}");
                if (lines.Count > 0 || attempt == _loadRetryCount)
                {
                    return lines;
                }
            }
            catch (Exception ex) when (attempt < _loadRetryCount)
            {
                WriteDiagnostics("load-error", title, artist, $"attempt={attempt}", ex.GetType().Name, ex.Message);
            }
            catch (Exception ex)
            {
                WriteDiagnostics("load-error", title, artist, $"attempt={attempt}", ex.GetType().Name, ex.Message);
                return [];
            }

            if (_loadRetryDelay > TimeSpan.Zero)
            {
                await Task.Delay(_loadRetryDelay, cancellationToken);
            }
        }

        return [];
    }

    private static string BuildCacheKey(string title, string artist)
    {
        return $"{Normalize(title)}|{Normalize(artist)}";
    }

    private bool ShouldRefresh(LyricsCacheEntry entry)
    {
        if (entry.Lines.Count > 0)
        {
            return false;
        }

        return _negativeCacheDuration <= TimeSpan.Zero
            || _utcNow() - entry.CachedAtUtc >= _negativeCacheDuration;
    }

    private static TimeSpan? EstimateDuration(IReadOnlyList<LrcLine> lines)
    {
        return lines.Count == 0
            ? null
            : lines[^1].Time + TimeSpan.FromSeconds(8);
    }

    private TimeSpan? EstimatePosition(string cacheKey, bool isPlaying, TimeSpan? duration)
    {
        if (duration is not { } validDuration || validDuration <= TimeSpan.Zero)
        {
            return null;
        }

        var now = _utcNow();
        var observation = _observations.AddOrUpdate(
            cacheKey,
            _ => new PlaybackObservation(TimeSpan.Zero, now, isPlaying),
            (_, previous) =>
            {
                var nextPosition = previous.Position;
                if (previous.IsPlaying)
                {
                    nextPosition += now - previous.ObservedAtUtc;
                }

                nextPosition = TimeSpan.FromMilliseconds(Math.Clamp(
                    nextPosition.TotalMilliseconds,
                    0,
                    validDuration.TotalMilliseconds));
                return new PlaybackObservation(nextPosition, now, isPlaying);
            });
        return observation.Position;
    }

    private static string ResolveDisplayLyric(
        IReadOnlyList<LrcLine> lines,
        TimeSpan? position,
        string title,
        string artist)
    {
        if (position is { } currentPosition)
        {
            var current = LrcParser.ResolveCurrentLine(lines, currentPosition);
            if (!IsNonLyricLine(current, title, artist))
            {
                return current;
            }
        }

        return lines
            .Select(line => line.Text)
            .FirstOrDefault(text => !IsNonLyricLine(text, title, artist))
            ?? string.Empty;
    }

    private static bool IsNonLyricLine(string? text, string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var normalized = text.Trim();
        return normalized.StartsWith("作词", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("作曲", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("编曲", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("制作人", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("监制", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("录音", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("混音", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("母带", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("和声", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("出品", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("发行", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("OP", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("SP", StringComparison.OrdinalIgnoreCase)
            || (normalized.EndsWith("：", StringComparison.Ordinal) && normalized.Length <= 20)
            || (normalized.EndsWith(":", StringComparison.Ordinal) && normalized.Length <= 20)
            || Normalize(normalized) == Normalize(title)
            || (!string.IsNullOrWhiteSpace(artist) && Normalize(normalized) == Normalize(artist));
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(character => !char.IsWhiteSpace(character))
            .ToArray());
    }

    private static string CleanTitle(string title, string artist)
    {
        var cleaned = title.Trim();
        var parts = cleaned.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && Normalize(artist).Contains(Normalize(parts[^1]), StringComparison.OrdinalIgnoreCase))
        {
            cleaned = string.Join(" - ", parts[..^1]);
        }

        cleaned = Regex.Replace(cleaned, @"\s*[（(][^（）()]*?(vip|mv|live|伴奏|吉他版|版|remix)[^（）()]*?[)）]\s*$", string.Empty, RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(cleaned) ? title.Trim() : cleaned.Trim();
    }

    private static void WriteDiagnostics(string eventName, string title, string artist, params string[] fields)
    {
        var diagnosticsPath = Environment.GetEnvironmentVariable("DYNAMIC_ISLAND_LYRICS_DIAGNOSTICS");
        if (string.IsNullOrWhiteSpace(diagnosticsPath))
        {
            return;
        }

        try
        {
            var line = string.Join(
                " | ",
                new[] { DateTimeOffset.Now.ToString("O"), eventName, title, artist }.Concat(fields));
            System.IO.File.AppendAllText(diagnosticsPath, line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics are best-effort only.
        }
    }

    private sealed record LyricsCacheEntry(IReadOnlyList<LrcLine> Lines, DateTime CachedAtUtc);

    private sealed record PlaybackObservation(TimeSpan Position, DateTime ObservedAtUtc, bool IsPlaying);
}
