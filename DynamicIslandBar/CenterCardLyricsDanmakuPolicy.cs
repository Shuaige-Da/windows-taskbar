namespace DynamicIslandBar;

public readonly record struct LyricLineMotionPlan(
    double CurrentStartOffset,
    double CurrentOffset,
    double CurrentEndOffset,
    double CurrentExitOffset,
    double NextStartOffset,
    double NextEndOffset,
    TimeSpan RemainingDuration,
    TimeSpan CurrentExitDuration,
    TimeSpan NextRevealDelay,
    TimeSpan NextRevealDuration);

public static class CenterCardLyricsDanmakuPolicy
{
    public static string FormatVerticalTrack(string lyric)
    {
        if (string.IsNullOrWhiteSpace(lyric))
        {
            return string.Empty;
        }

        return FormatVerticalLine(lyric);
    }

    private static string FormatVerticalLine(string lyric)
    {
        var characters = lyric
            .Where(c => !char.IsWhiteSpace(c))
            .Select(c => c.ToString());
        return string.Join(Environment.NewLine, characters);
    }

    public static LyricLineMotionPlan BuildLineMotionPlan(
        double viewportExtent,
        double currentTextExtent,
        double nextTextExtent,
        TimeSpan lineDuration,
        double progress)
    {
        var viewport = Math.Max(viewportExtent, 1d);
        var currentExtent = Math.Max(currentTextExtent, 1d);
        var nextExtent = Math.Max(nextTextExtent, 1d);
        var normalizedProgress = Math.Clamp(progress, 0d, 1d);
        var currentStart = Math.Max(12d, viewport * 0.62d);
        var availableExtent = Math.Max(viewport - 24d, 1d);
        var currentEnd = currentExtent > availableExtent
            ? viewport - currentExtent - 12d
            : Math.Max(12d, currentStart - Math.Min(viewport * 0.28d, 80d));
        var currentOffset = currentStart + ((currentEnd - currentStart) * normalizedProgress);
        var currentExit = -(currentExtent + 24d);

        var duration = lineDuration > TimeSpan.Zero ? lineDuration : TimeSpan.FromSeconds(4);
        var remainingSeconds = Math.Max(0.15d, duration.TotalSeconds * (1d - normalizedProgress));
        var revealLeadSeconds = Math.Clamp(duration.TotalSeconds * 0.28d, 0.8d, 1.5d);
        var revealDelaySeconds = Math.Max(0d, remainingSeconds - revealLeadSeconds);
        var revealDurationSeconds = Math.Max(
            0.2d,
            Math.Min(revealLeadSeconds, remainingSeconds));
        var exitDurationSeconds = Math.Clamp(
            (currentEnd - currentExit) / 180d,
            0.8d,
            1.6d);
        var nextStart = viewport + 12d;
        var nextEnd = Math.Max(
            viewport * 0.62d,
            viewport - Math.Min(nextExtent, viewport * 0.34d) - 12d);

        return new LyricLineMotionPlan(
            currentStart,
            currentOffset,
            currentEnd,
            currentExit,
            nextStart,
            nextEnd,
            TimeSpan.FromSeconds(remainingSeconds),
            TimeSpan.FromSeconds(exitDurationSeconds),
            TimeSpan.FromSeconds(revealDelaySeconds),
            TimeSpan.FromSeconds(revealDurationSeconds));
    }
}
