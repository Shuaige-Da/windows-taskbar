namespace DynamicIslandBar;

public readonly record struct LyricSearchIdentity(
    string Title,
    string Artist,
    TimeSpan Duration);

public sealed record LyricCandidate(
    string Provider,
    string Id,
    string Title,
    string Artist,
    TimeSpan Duration,
    bool HasSyncedLyrics,
    bool HasPlainLyrics,
    bool IsNoLyric,
    bool IsUncollected);

public static class LyricMatchingPolicy
{
    public const double MinimumAcceptedScore = 0.55d;

    private static readonly string[] VersionPenaltyTerms =
    [
        "live",
        "dj",
        "remix",
        "cover",
        "demo",
        "instrumental",
        "karaoke",
        "acoustic",
        "伴奏",
        "翻唱",
        "现场",
        "混音",
        "纯音乐"
    ];

    public static LyricCandidate? SelectBestCandidate(
        LyricSearchIdentity identity,
        IEnumerable<LyricCandidate> candidates)
    {
        return candidates
            .Where(CanUse)
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = Score(identity, candidate)
            })
            .Where(result => result.Score >= MinimumAcceptedScore)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => GetDurationDifference(identity.Duration, result.Candidate.Duration))
            .Select(result => result.Candidate)
            .FirstOrDefault();
    }

    public static double Score(LyricSearchIdentity identity, LyricCandidate candidate)
    {
        if (!CanUse(candidate))
        {
            return 0d;
        }

        var titleScore = Similarity(Normalize(identity.Title), Normalize(candidate.Title));
        var artistScore = string.IsNullOrWhiteSpace(identity.Artist)
            ? 0.75d
            : Similarity(Normalize(identity.Artist), Normalize(candidate.Artist));
        var durationPenalty = CalculateDurationPenalty(identity.Duration, candidate.Duration);
        var versionPenalty = ContainsVersionPenalty(candidate.Title) ? 0.18d : 0d;
        var lyricBonus = candidate.HasSyncedLyrics ? 0.08d : candidate.HasPlainLyrics ? 0.03d : 0d;

        return Math.Max(
            0d,
            (titleScore * 0.52d)
            + (artistScore * 0.35d)
            + lyricBonus
            - durationPenalty
            - versionPenalty);
    }

    private static bool CanUse(LyricCandidate candidate)
    {
        return !candidate.IsNoLyric
            && !candidate.IsUncollected
            && (candidate.HasSyncedLyrics || candidate.HasPlainLyrics);
    }

    private static double CalculateDurationPenalty(TimeSpan expected, TimeSpan actual)
    {
        if (expected <= TimeSpan.Zero || actual <= TimeSpan.Zero)
        {
            return 0d;
        }

        return Math.Min(GetDurationDifference(expected, actual) / 30d, 0.35d);
    }

    private static double GetDurationDifference(TimeSpan expected, TimeSpan actual)
    {
        return Math.Abs((actual - expected).TotalSeconds);
    }

    private static bool ContainsVersionPenalty(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
        return VersionPenaltyTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        foreach (var term in VersionPenaltyTerms)
        {
            normalized = normalized.Replace(term, string.Empty, StringComparison.Ordinal);
        }

        return new string(normalized
            .Where(c => char.IsLetterOrDigit(c) || IsCjk(c))
            .ToArray());
    }

    private static bool IsCjk(char value)
    {
        return value >= '\u4e00' && value <= '\u9fff';
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0d;
        }

        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 1d;
        }

        if (left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal))
        {
            return 0.92d;
        }

        var overlap = left.Intersect(right).Count();
        var longest = Math.Max(left.Length, right.Length);
        return longest == 0 ? 0d : Math.Min((double)overlap / longest, 0.72d);
    }
}
