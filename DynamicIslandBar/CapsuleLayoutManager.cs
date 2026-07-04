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
    public static CapsuleSnapPreview BuildSnapPreview(
        SnapEdge edge,
        double screenWidth,
        double screenHeight,
        double topCapsuleWidth,
        double topCapsuleHeight,
        double bottomCapsuleWidth,
        double bottomCapsuleHeight)
    {
        var (mode, capsuleWidth, capsuleHeight, rotationDegrees) = edge switch
        {
            SnapEdge.Top => (CapsuleMode.TopIsland, topCapsuleWidth, topCapsuleHeight, 0d),
            SnapEdge.Left => (CapsuleMode.LeftDock, topCapsuleWidth, topCapsuleHeight, 90d),
            SnapEdge.Right => (CapsuleMode.RightDock, topCapsuleWidth, topCapsuleHeight, 90d),
            SnapEdge.Bottom => (CapsuleMode.BottomTaskbar, bottomCapsuleWidth, bottomCapsuleHeight, 0d),
            _ => (CapsuleMode.Floating, 0d, 0d, 0d)
        };

        var frame = edge switch
        {
            SnapEdge.Left or SnapEdge.Right => GetWindowFrame(
                mode,
                new LayoutMetrics(capsuleHeight, capsuleWidth, VisibleAppSlots: 0, PopupFlowDirection.Up),
                screenWidth,
                screenHeight),
            SnapEdge.None => default,
            _ => GetWindowFrame(
                mode,
                new LayoutMetrics(capsuleWidth, capsuleHeight, VisibleAppSlots: 0, PopupFlowDirection.Up),
                screenWidth,
                screenHeight)
        };

        return new CapsuleSnapPreview(edge, mode, capsuleWidth, capsuleHeight, rotationDegrees, frame);
    }

    public static LayoutMetrics GetMetrics(CapsuleMode mode, double screenWidth, double screenHeight)
    {
        return mode switch
        {
            CapsuleMode.TopIsland => new LayoutMetrics(
                CapsuleWidth: 760,
                CapsuleHeight: 72,
                VisibleAppSlots: 3,
                PopupDirection: PopupFlowDirection.Down),
            CapsuleMode.LeftDock or CapsuleMode.RightDock => new LayoutMetrics(
                CapsuleWidth: Math.Max(screenWidth * 0.08, 96),
                CapsuleHeight: screenHeight * (2d / 3d),
                VisibleAppSlots: 10,
                PopupDirection: PopupFlowDirection.Up),
            _ => new LayoutMetrics(
                CapsuleWidth: screenWidth,
                CapsuleHeight: 80,
                VisibleAppSlots: 8,
                PopupDirection: PopupFlowDirection.Up)
        };
    }

    public static CapsuleMode ResolveDropMode(
        double screenWidth,
        double screenHeight,
        double leftAfterDrag,
        double topAfterDrag,
        CapsuleMode currentMode)
    {
        if (leftAfterDrag <= 72)
        {
            return CapsuleMode.LeftDock;
        }

        if (leftAfterDrag >= screenWidth - 72)
        {
            return CapsuleMode.RightDock;
        }

        if (topAfterDrag <= 72)
        {
            return CapsuleMode.TopIsland;
        }

        if (topAfterDrag >= screenHeight - 72)
        {
            return CapsuleMode.BottomTaskbar;
        }

        return CapsuleMode.Floating;
    }

    public static WindowFrame GetWindowFrame(
        CapsuleMode mode,
        LayoutMetrics metrics,
        double screenWidth,
        double screenHeight)
    {
        var windowWidth = metrics.CapsuleWidth + 40;
        var windowHeight = 420d;
        var left = (screenWidth - windowWidth) / 2;
        var capsuleBottomOffset = ((windowHeight - metrics.CapsuleHeight) / 2) + metrics.CapsuleHeight;
        var top = mode == CapsuleMode.TopIsland
            ? 0
            : Math.Max(screenHeight - capsuleBottomOffset, 0);

        if (mode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            windowWidth = metrics.CapsuleWidth + 24;
            windowHeight = metrics.CapsuleHeight + 40;
            left = mode == CapsuleMode.LeftDock ? 0 : Math.Max(screenWidth - windowWidth, 0);
            top = Math.Max((screenHeight - windowHeight) / 2, 0);
        }

        return new WindowFrame(left, top, windowWidth, windowHeight);
    }
}
