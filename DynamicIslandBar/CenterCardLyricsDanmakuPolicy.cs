namespace DynamicIslandBar;

public readonly record struct LyricsDanmakuRegistration(
    bool ShouldEnqueue,
    int LaneIndex,
    int NextLaneIndex);

public static class CenterCardLyricsDanmakuPolicy
{
    public const string ContinuousTrackGap = "\u3000\u3000\u3000\u3000";

    public static LyricsDanmakuRegistration RegisterLyric(
        string? previousLyric,
        string? newLyric,
        int nextLaneIndex,
        int laneCount)
    {
        if (laneCount <= 0 || string.IsNullOrWhiteSpace(newLyric))
        {
            return new LyricsDanmakuRegistration(false, -1, Math.Max(0, nextLaneIndex));
        }

        if (string.Equals(previousLyric, newLyric, StringComparison.Ordinal))
        {
            return new LyricsDanmakuRegistration(false, -1, Math.Max(0, nextLaneIndex));
        }

        var laneIndex = Math.Clamp(nextLaneIndex, 0, laneCount - 1);
        return new LyricsDanmakuRegistration(
            true,
            laneIndex,
            (laneIndex + 1) % laneCount);
    }

    public static string BuildContinuousTrack(IEnumerable<string?> lyricLines)
    {
        var lines = lyricLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line!.Trim())
            .ToArray();

        return string.Join(ContinuousTrackGap, lines);
    }

    public static string FormatVerticalTrack(string lyric)
    {
        if (string.IsNullOrWhiteSpace(lyric))
        {
            return string.Empty;
        }

        var lines = lyric.Split(ContinuousTrackGap, StringSplitOptions.None)
            .Select(FormatVerticalLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join(CreateVerticalGap(4), lines);
    }

    private static string FormatVerticalLine(string lyric)
    {
        var characters = lyric
            .Where(c => !char.IsWhiteSpace(c))
            .Select(c => c.ToString());
        return string.Join(Environment.NewLine, characters);
    }

    private static string CreateVerticalGap(int blankLineCount)
    {
        return string.Concat(Enumerable.Repeat(Environment.NewLine, blankLineCount + 1));
    }

    public static TimeSpan CalculateSynchronizedTrackDuration(
        TimeSpan currentLyricDuration,
        double totalTravelDistance,
        double currentLyricVisibleDistance,
        TimeSpan fallbackDuration)
    {
        if (currentLyricDuration <= TimeSpan.Zero
            || totalTravelDistance <= 0
            || currentLyricVisibleDistance <= 0)
        {
            return fallbackDuration;
        }

        var multiplier = totalTravelDistance / currentLyricVisibleDistance;
        return TimeSpan.FromSeconds(currentLyricDuration.TotalSeconds * multiplier);
    }

    public static bool ShouldRestartMarquee(
        bool isActive,
        bool usesVerticalLyricsFlow,
        int activeTrackCount,
        string? activeText,
        string? nextText)
    {
        if (string.IsNullOrWhiteSpace(nextText))
        {
            return false;
        }

        if (!isActive)
        {
            return true;
        }

        if (string.Equals(activeText, nextText, StringComparison.Ordinal))
        {
            return false;
        }

        return activeTrackCount <= 0;
    }
}
