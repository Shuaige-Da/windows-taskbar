namespace DynamicIslandBar;

public enum PopupFlowDirection
{
    Up,
    Down,
    Left,
    Right
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
                VisibleAppSlots: 3,
                PopupDirection: PopupFlowDirection.Down),
            CapsuleMode.LeftDock => new LayoutMetrics(
                CapsuleWidth: screenHeight,
                CapsuleHeight: 72,
                VisibleAppSlots: 3,
                PopupDirection: PopupFlowDirection.Right),
            CapsuleMode.RightDock => new LayoutMetrics(
                CapsuleWidth: screenHeight,
                CapsuleHeight: 72,
                VisibleAppSlots: 3,
                PopupDirection: PopupFlowDirection.Left),
            _ => new LayoutMetrics(
                CapsuleWidth: screenWidth,
                CapsuleHeight: 80,
                VisibleAppSlots: 8,
                PopupDirection: PopupFlowDirection.Up)
        };
    }

    public static CapsuleMode ResolveDropMode(
        double screenWidth, double screenHeight,
        double leftAfterDrag, double topAfterDrag,
        CapsuleMode currentMode)
    {
        if (topAfterDrag <= 72)
        {
            return CapsuleMode.TopIsland;
        }

        if (leftAfterDrag <= 0)
        {
            return CapsuleMode.LeftDock;
        }

        if (leftAfterDrag >= screenWidth - 200)
        {
            return CapsuleMode.RightDock;
        }

        if (currentMode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            return currentMode;
        }

        return CapsuleMode.BottomTaskbar;
    }

    public static WindowFrame GetWindowFrame(
        CapsuleMode mode,
        LayoutMetrics metrics,
        double screenWidth,
        double screenHeight)
    {
        if (mode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            // 竖向长度 = 屏幕高度的 2/3
            var windowHeight = Math.Min(screenHeight * 2.0 / 3.0, 760);
            // 窗口宽度需要足够容纳竖向胶囊粗细 + 内容
            const double windowWidth = 200;
            var top = (screenHeight - windowHeight) / 2;
            var left = mode == CapsuleMode.LeftDock
                ? 0
                : Math.Max(screenWidth - windowWidth, 0);

            return new WindowFrame(left, top, windowWidth, windowHeight);
        }

        var windowWidthH = metrics.CapsuleWidth + 40;
        const double windowHeightH = 420;
        var leftH = (screenWidth - windowWidthH) / 2;
        var capsuleBottomOffset = ((windowHeightH - metrics.CapsuleHeight) / 2) + metrics.CapsuleHeight;
        var topH = mode == CapsuleMode.TopIsland
            ? 0
            : Math.Max(screenHeight - capsuleBottomOffset, 0);

        return new WindowFrame(leftH, topH, windowWidthH, windowHeightH);
    }
}
