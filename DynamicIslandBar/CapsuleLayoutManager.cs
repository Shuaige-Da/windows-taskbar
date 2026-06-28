namespace DynamicIslandBar;

public enum PopupFlowDirection
{
    Up,
    Down
}

public readonly record struct LayoutMetrics(
    double CapsuleWidth,
    double CapsuleHeight,
    int VisibleAppSlots,
    PopupFlowDirection PopupDirection);

public readonly record struct WindowFrame(
    double Left,
    double Top,
    double Width,
    double Height);

public static class CapsuleLayoutManager
{
    public static LayoutMetrics GetMetrics(CapsuleMode mode, double screenWidth, double screenHeight)
    {
        return mode switch
        {
            CapsuleMode.TopIsland => new LayoutMetrics(
                CapsuleWidth: 760,
                CapsuleHeight: 72,
                VisibleAppSlots: 5,
                PopupDirection: PopupFlowDirection.Down),
            _ => new LayoutMetrics(
                CapsuleWidth: screenWidth,
                CapsuleHeight: 80,
                VisibleAppSlots: 8,
                PopupDirection: PopupFlowDirection.Up)
        };
    }

    public static CapsuleMode ResolveDropMode(double screenHeight, double topAfterDrag, CapsuleMode currentMode)
    {
        if (topAfterDrag <= 72)
        {
            return CapsuleMode.TopIsland;
        }

        return CapsuleMode.BottomTaskbar;
    }

    public static WindowFrame GetWindowFrame(
        CapsuleMode mode,
        LayoutMetrics metrics,
        double screenWidth,
        double screenHeight)
    {
        var windowWidth = metrics.CapsuleWidth + 40;
        const double windowHeight = 420;
        var left = (screenWidth - windowWidth) / 2;
        var capsuleBottomOffset = ((windowHeight - metrics.CapsuleHeight) / 2) + metrics.CapsuleHeight;
        var top = mode == CapsuleMode.TopIsland
            ? 0
            : Math.Max(screenHeight - capsuleBottomOffset, 0);

        return new WindowFrame(left, top, windowWidth, windowHeight);
    }
}
