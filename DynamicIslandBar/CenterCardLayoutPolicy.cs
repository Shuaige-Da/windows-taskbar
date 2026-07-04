namespace DynamicIslandBar;

public enum CenterCardTransportDensity
{
    Full,
    Compact,
    Minimal
}

public readonly record struct CenterCardLyricsLayout(
    double HorizontalMargin,
    bool ShowLeftWave,
    bool ShowRightWave);

public static class CenterCardLayoutPolicy
{
    private const double MinWidthRatio = 0.18d;
    private const double MaxWidthRatio = 0.40d;
    private const double CenterCardOuterHorizontalMargin = 36d;
    private const double CenterSlotSafetyInset = 16d;

    public static double MapWidth(CapsuleMode mode, double capsuleWidth, int percent)
    {
        if (capsuleWidth <= 0)
        {
            return 0;
        }

        var ratio = MapPercentToRatio(percent);
        return capsuleWidth * ratio;
    }

    public static double MapWidth(
        CapsuleMode mode,
        double capsuleWidth,
        int percent,
        double availableCenterSlotWidth)
    {
        var ratioWidth = MapWidth(mode, capsuleWidth, percent);
        if (availableCenterSlotWidth <= 0)
        {
            return ratioWidth;
        }

        var safeSlotWidth = Math.Max(
            0,
            availableCenterSlotWidth - CenterCardOuterHorizontalMargin - CenterSlotSafetyInset);
        return Math.Min(ratioWidth, safeSlotWidth);
    }

    public static int MapWidthPercent(CapsuleMode mode, double capsuleWidth, double width)
    {
        if (capsuleWidth <= 0)
        {
            return 0;
        }

        var ratio = width / capsuleWidth;
        return (int)Math.Round(Math.Clamp((ratio - MinWidthRatio) / (MaxWidthRatio - MinWidthRatio), 0, 1) * 100);
    }

    private static double MapPercentToRatio(int percent)
    {
        var normalized = Math.Clamp(percent, 0, 100) / 100.0;
        return MinWidthRatio + ((MaxWidthRatio - MinWidthRatio) * normalized);
    }

    public static CenterCardTransportDensity GetTransportDensity(double centerCardWidth)
    {
        if (centerCardWidth < 320)
        {
            return CenterCardTransportDensity.Minimal;
        }

        return centerCardWidth < 430
            ? CenterCardTransportDensity.Compact
            : CenterCardTransportDensity.Full;
    }

    public static CenterCardLyricsLayout GetLyricsLayout(double centerCardWidth)
    {
        return GetTransportDensity(centerCardWidth) switch
        {
            CenterCardTransportDensity.Minimal => new CenterCardLyricsLayout(24, false, false),
            CenterCardTransportDensity.Compact => new CenterCardLyricsLayout(30, true, false),
            _ => new CenterCardLyricsLayout(42, true, true)
        };
    }

    public static double MapSideDockExtent(double mappedTopLength, double availableHeight)
    {
        if (availableHeight <= 0)
        {
            return 96d;
        }

        if (availableHeight <= 96d)
        {
            return availableHeight;
        }

        return Math.Clamp(mappedTopLength, 96d, availableHeight);
    }
}
