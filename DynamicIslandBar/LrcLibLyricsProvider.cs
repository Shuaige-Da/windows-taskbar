using System.Net.Http;
using System.Text.Json;

namespace DynamicIslandBar;

public sealed class LrcLibLyricsProvider : ILyricsProvider
{
    private static readonly HttpClient SharedClient = CreateClient();

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

        var candidates = await SearchAsync(title, artist, cancellationToken);
        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = ScoreCandidate(candidate, title, artist, duration)
            })
            .Where(match => match.Score > 0.35 && !string.IsNullOrWhiteSpace(match.Candidate.SyncedLyrics))
            .OrderByDescending(match => match.Score)
            .Select(match => match.Candidate.SyncedLyrics)
            .FirstOrDefault();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIslandBar/1.0 (windows-taskbar)");
        return client;
    }

    private static async Task<IReadOnlyList<LrcLibCandidate>> SearchAsync(
        string title,
        string artist,
        CancellationToken cancellationToken)
    {
        var urls = string.IsNullOrWhiteSpace(artist)
            ? [BuildSearchUrl(title, null)]
            : new[] { BuildSearchUrl(title, artist), BuildSearchUrl(title, null) };
        var candidates = new List<LrcLibCandidate>();

        foreach (var url in urls)
        {
            try
            {
                using var response = await SharedClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    candidates.Add(ReadCandidate(item));
                }
            }
            catch
            {
                // Lyrics are opportunistic; a failed source must not break the center card.
            }
        }

        return candidates;
    }

    private static string BuildSearchUrl(string title, string? artist)
    {
        var query = $"track_name={Uri.EscapeDataString(title)}";
        if (!string.IsNullOrWhiteSpace(artist))
        {
            query += $"&artist_name={Uri.EscapeDataString(artist)}";
        }

        return $"https://lrclib.net/api/search?{query}";
    }

    private static LrcLibCandidate ReadCandidate(JsonElement item)
    {
        var duration = item.TryGetProperty("duration", out var durationProperty)
            && durationProperty.ValueKind == JsonValueKind.Number
                ? TimeSpan.FromSeconds(durationProperty.GetDouble())
                : (TimeSpan?)null;
        return new LrcLibCandidate(
            ReadString(item, "trackName"),
            ReadString(item, "artistName"),
            duration,
            ReadString(item, "syncedLyrics"));
    }

    private static string ReadString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static double ScoreCandidate(
        LrcLibCandidate candidate,
        string expectedTitle,
        string expectedArtist,
        TimeSpan? expectedDuration)
    {
        var titleScore = ScoreText(candidate.Title, expectedTitle);
        var artistScore = string.IsNullOrWhiteSpace(expectedArtist)
            ? 0.5
            : ScoreText(candidate.Artist, expectedArtist);
        var durationScore = ScoreDuration(candidate.Duration, expectedDuration);
        return (titleScore * 0.55) + (artistScore * 0.25) + (durationScore * 0.20);
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

    private sealed record LrcLibCandidate(string Title, string Artist, TimeSpan? Duration, string SyncedLyrics);
}
