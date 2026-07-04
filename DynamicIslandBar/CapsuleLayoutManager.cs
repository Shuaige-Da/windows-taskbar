using System.Windows;

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
    private const double DropSnapThreshold = 72;
    private const double FloatingWindowHorizontalPadding = 20;
    private const double FloatingWindowHeight = 420;
    private const double SideDockHorizontalPadding = 12;
    private const double SideDockVerticalPadding = 20;

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
                CapsuleWidth: 760,
                CapsuleHeight: 72,
                VisibleAppSlots: 3,
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
        double topAfterDrag)
    {
        return ResolveDropMode(screenWidth, screenHeight, new Point(leftAfterDrag, topAfterDrag));
    }

    public static CapsuleMode ResolveDropMode(
        double screenWidth,
        double screenHeight,
        Point cursorScreenPoint)
    {
        if (cursorScreenPoint.X <= DropSnapThreshold)
        {
            return CapsuleMode.LeftDock;
        }

        if (cursorScreenPoint.X >= screenWidth - DropSnapThreshold)
        {
            return CapsuleMode.RightDock;
        }

        if (cursorScreenPoint.Y <= DropSnapThreshold)
        {
            return CapsuleMode.TopIsland;
        }

        if (cursorScreenPoint.Y >= screenHeight - DropSnapThreshold)
        {
            return CapsuleMode.BottomTaskbar;
        }

        return CapsuleMode.Floating;
    }

    public static Size ResolveBottomPreviewCapsuleSize(
        double fallbackWidth,
        double fallbackHeight,
        double lastBottomCapsuleWidth,
        double lastBottomCapsuleHeight)
    {
        if (lastBottomCapsuleWidth > 0 && lastBottomCapsuleHeight > 0)
        {
            return new Size(lastBottomCapsuleWidth, lastBottomCapsuleHeight);
        }

        return new Size(fallbackWidth, fallbackHeight);
    }

    public static WindowFrame GetWindowFrame(
        CapsuleMode mode,
        LayoutMetrics metrics,
        double screenWidth,
        double screenHeight,
        double floatingLeft = 0,
        double floatingTop = 0)
    {
        var windowWidth = metrics.CapsuleWidth + (FloatingWindowHorizontalPadding * 2);
        var windowHeight = FloatingWindowHeight;
        var left = (screenWidth - windowWidth) / 2;
        var capsuleBottomOffset = ((windowHeight - metrics.CapsuleHeight) / 2) + metrics.CapsuleHeight;
        var top = mode == CapsuleMode.TopIsland
            ? 0
            : Math.Max(screenHeight - capsuleBottomOffset, 0);

        if (mode == CapsuleMode.Floating)
        {
            left = floatingLeft;
            top = floatingTop;
        }

        if (mode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            windowWidth = metrics.CapsuleHeight + (SideDockHorizontalPadding * 2);
            windowHeight = metrics.CapsuleWidth + (SideDockVerticalPadding * 2);
            left = mode == CapsuleMode.LeftDock ? 0 : Math.Max(screenWidth - windowWidth, 0);
            top = Math.Max((screenHeight - windowHeight) / 2, 0);
        }

        return new WindowFrame(left, top, windowWidth, windowHeight);
    }

    public static Rect GetCapsuleBounds(
        CapsuleMode mode,
        WindowFrame frame,
        double renderedCapsuleWidth,
        double renderedCapsuleHeight)
    {
        if (mode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            return new Rect(
                frame.Left + SideDockHorizontalPadding,
                frame.Top + SideDockVerticalPadding,
                renderedCapsuleHeight,
                renderedCapsuleWidth);
        }

        if (mode == CapsuleMode.TopIsland)
        {
            return new Rect(
                frame.Left + FloatingWindowHorizontalPadding,
                frame.Top,
                renderedCapsuleWidth,
                renderedCapsuleHeight);
        }

        return new Rect(
            frame.Left + FloatingWindowHorizontalPadding,
            frame.Top + ((frame.Height - renderedCapsuleHeight) / 2),
            renderedCapsuleWidth,
            renderedCapsuleHeight);
    }

    public static Point GetFloatingWindowOriginForVisibleCapsule(
        double renderedFloatingCapsuleWidth,
        double renderedFloatingCapsuleHeight,
        double visibleCapsuleLeft,
        double visibleCapsuleTop)
    {
        return new Point(
            visibleCapsuleLeft - FloatingWindowHorizontalPadding,
            visibleCapsuleTop - ((FloatingWindowHeight - renderedFloatingCapsuleHeight) / 2));
    }
}
