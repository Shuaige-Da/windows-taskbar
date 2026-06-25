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

public static class CapsuleLayoutManager
{
    public static LayoutMetrics GetMetrics(CapsuleMode mode, double screenWidth, double screenHeight)
    {
        return mode switch
        {
            CapsuleMode.TopIsland => new LayoutMetrics(
                CapsuleWidth: 760,
                CapsuleHeight: 64,
                VisibleAppSlots: 5,
                PopupDirection: PopupFlowDirection.Down),
            _ => new LayoutMetrics(
                CapsuleWidth: Math.Min(screenWidth - 120, 1380),
                CapsuleHeight: 72,
                VisibleAppSlots: 12,
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
}
