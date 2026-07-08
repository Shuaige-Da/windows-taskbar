namespace DynamicIslandBar;

public readonly record struct CenterCardLyricScrollPlan(
    double StartOffset,
    double EndOffset,
    double Distance,
    TimeSpan Duration);

public static class CenterCardLyricScrollPolicy
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1.8);
    private static readonly TimeSpan FallbackDuration = TimeSpan.FromSeconds(3);

    public static CenterCardLyricScrollPlan BuildHorizontalPlan(
        double viewportWidth,
        double textWidth,
        TimeSpan lineLifetime)
    {
        var safeViewportWidth = Math.Max(viewportWidth, 1d);
        var safeTextWidth = Math.Max(textWidth, 1d);
        var duration = ResolveDuration(lineLifetime);
        var startOffset = Math.Max(safeViewportWidth - safeTextWidth, 0d);
        var endOffset = -(safeTextWidth + 36d);

        return new CenterCardLyricScrollPlan(
            StartOffset: startOffset,
            EndOffset: endOffset,
            Distance: startOffset - endOffset,
            Duration: duration);
    }

    public static CenterCardLyricScrollPlan BuildVerticalPlan(
        double viewportHeight,
        double textHeight,
        TimeSpan lineLifetime)
    {
        var safeViewportHeight = Math.Max(viewportHeight, 1d);
        var safeTextHeight = Math.Max(textHeight, 1d);
        var duration = ResolveDuration(lineLifetime);
        var distance = Math.Max(safeTextHeight + 28d, safeViewportHeight * 0.35d);

        return new CenterCardLyricScrollPlan(
            StartOffset: safeViewportHeight + 2d,
            EndOffset: -distance,
            Distance: distance,
            Duration: duration);
    }

    private static TimeSpan ResolveDuration(TimeSpan lineLifetime)
    {
        if (lineLifetime <= TimeSpan.Zero)
        {
            return FallbackDuration;
        }

        return lineLifetime < MinimumDuration
            ? MinimumDuration
            : lineLifetime;
    }
}
