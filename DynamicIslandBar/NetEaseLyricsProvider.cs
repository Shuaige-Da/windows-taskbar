using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DynamicIslandBar;

public sealed class NetEaseLyricsProvider : ILyricsProvider
{
    private static readonly HttpClient SharedClient = CreateClient();
    private static readonly Regex TimestampRegex = new(@"\[(?<timestamp>\d{1,3}:\d{2}(?:[\.,]\d{1,3})?)\]", RegexOptions.Compiled);
    private readonly HttpClient _client;

    public NetEaseLyricsProvider()
        : this(SharedClient)
    {
    }

    public NetEaseLyricsProvider(HttpClient client)
    {
        _client = client;
    }

    public async Task<string?> TryGetLrcAsync(
        string title,
        string artist,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var songs = await SearchSongsAsync($"{title} {artist}", cancellationToken);
        var bestSongs = songs
            .Select(song => new
            {
                Song = song,
                Score = ScoreSong(song, title, artist, duration)
            })
            .Where(match => match.Score > 0.35)
            .OrderByDescending(match => match.Score)
            .Select(match => match.Song);

        foreach (var song in bestSongs)
        {
            var lyric = await FetchLyricAsync(song.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(lyric))
            {
                return lyric;
            }
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 DynamicIslandBar/1.0");
        client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
        return client;
    }

    private async Task<IReadOnlyList<NetEaseSongCandidate>> SearchSongsAsync(
        string keyword,
        CancellationToken cancellationToken)
    {
        var url = "https://music.163.com/api/search/get"
            + $"?s={Uri.EscapeDataString(keyword)}&type=1&limit=10&offset=0&total=false";
        using var response = await _client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("result", out var result)
            || !result.TryGetProperty("songs", out var songs)
            || songs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var candidates = new List<NetEaseSongCandidate>();
        foreach (var song in songs.EnumerateArray())
        {
            if (!song.TryGetProperty("id", out var idProperty)
                || !idProperty.TryGetInt64(out var id)
                || !song.TryGetProperty("name", out var nameProperty))
            {
                continue;
            }

            var durationValue = song.TryGetProperty("duration", out var durationProperty)
                && durationProperty.TryGetInt32(out var durationMs)
                    ? TimeSpan.FromMilliseconds(durationMs)
                    : (TimeSpan?)null;
            candidates.Add(new NetEaseSongCandidate(
                id,
                nameProperty.GetString() ?? string.Empty,
                ReadArtists(song),
                durationValue));
        }

        return candidates;
    }

    private async Task<string?> FetchLyricAsync(long songId, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(
            $"https://music.163.com/api/song/lyric?id={songId}&lv=1&kv=1&tv=-1",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("lrc", out var lrc)
            || !lrc.TryGetProperty("lyric", out var lyric))
        {
            return null;
        }

        var baseLyric = lyric.GetString();
        var translatedLyric = document.RootElement.TryGetProperty("tlyric", out var tlyric)
            && tlyric.TryGetProperty("lyric", out var translation)
                ? translation.GetString()
                : null;
        return MergeTranslatedLyrics(baseLyric, translatedLyric);
    }

    private static string? MergeTranslatedLyrics(string? baseLyric, string? translatedLyric)
    {
        if (string.IsNullOrWhiteSpace(baseLyric))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(translatedLyric))
        {
            return baseLyric;
        }

        var translations = ReadTimestampedText(translatedLyric);
        if (translations.Count == 0)
        {
            return baseLyric;
        }

        var mergedLines = new List<string>();
        foreach (var rawLine in baseLyric.Replace("\r\n", "\n").Split('\n'))
        {
            var matches = TimestampRegex.Matches(rawLine);
            if (matches.Count == 0)
            {
                mergedLines.Add(rawLine);
                continue;
            }

            var baseText = TimestampRegex.Replace(rawLine, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseText))
            {
                mergedLines.Add(rawLine);
                continue;
            }

            var translation = matches
                .Select(match => NormalizeTimestamp(match.Groups["timestamp"].Value))
                .Select(timestamp => translations.TryGetValue(timestamp, out var text) ? text : null)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            mergedLines.Add(string.IsNullOrWhiteSpace(translation) || string.Equals(baseText, translation, StringComparison.OrdinalIgnoreCase)
                ? rawLine
                : $"{string.Concat(matches.Select(match => match.Value))}{baseText} / {translation}");
        }

        return string.Join('\n', mergedLines);
    }

    private static Dictionary<string, string> ReadTimestampedText(string lrc)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in lrc.Replace("\r\n", "\n").Split('\n'))
        {
            var matches = TimestampRegex.Matches(rawLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex.Replace(rawLine, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                result[NormalizeTimestamp(match.Groups["timestamp"].Value)] = text;
            }
        }

        return result;
    }

    private static string NormalizeTimestamp(string timestamp)
    {
        var normalized = timestamp.Replace(',', '.');
        var parts = normalized.Split('.');
        var fraction = parts.Length > 1 ? parts[1] : string.Empty;
        fraction = fraction.Length switch
        {
            0 => "000",
            1 => fraction + "00",
            2 => fraction + "0",
            _ => fraction[..3]
        };
        return $"{parts[0]}.{fraction}";
    }

    private static string ReadArtists(JsonElement song)
    {
        if (!song.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            artists.EnumerateArray()
                .Select(artist => artist.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    private static double ScoreSong(
        NetEaseSongCandidate song,
        string expectedTitle,
        string expectedArtist,
        TimeSpan? expectedDuration)
    {
        var titleScore = ScoreText(song.Title, expectedTitle);
        var artistScore = string.IsNullOrWhiteSpace(expectedArtist)
            ? 0.5
            : ScoreText(song.Artist, expectedArtist);
        var durationScore = ScoreDuration(song.Duration, expectedDuration);
        return (titleScore * 0.55) + (artistScore * 0.3) + (durationScore * 0.15);
    }

    private static double ScoreText(string candidate, string expected)
    {
        var normalizedCandidate = Normalize(candidate);
        var normalizedExpected = Normalize(expected);
        if (string.IsNullOrWhiteSpace(normalizedCandidate) || string.IsNullOrWhiteSpace(normalizedExpected))
        {
            return 0;
        }

        if (normalizedCandidate == normalizedExpected)
        {
            return 1;
        }

        return normalizedCandidate.Contains(normalizedExpected) || normalizedExpected.Contains(normalizedCandidate)
            ? 0.72
            : 0;
    }

    private static double ScoreDuration(TimeSpan? candidate, TimeSpan? expected)
    {
        if (candidate is not { } candidateDuration || expected is not { } expectedDuration)
        {
            return 0.5;
        }

        var delta = Math.Abs((candidateDuration - expectedDuration).TotalSeconds);
        return delta switch
        {
            <= 2 => 1,
            <= 8 => 0.75,
            <= 20 => 0.35,
            _ => 0
        };
    }

    private static string Normalize(string value)
    {
        return new string(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private sealed record NetEaseSongCandidate(long Id, string Title, string Artist, TimeSpan? Duration);
}
