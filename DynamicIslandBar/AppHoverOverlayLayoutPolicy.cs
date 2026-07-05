namespace DynamicIslandBar;

public readonly record struct AppHoverOverlayFrame(double Left, double Top);

public static class AppHoverOverlayLayoutPolicy
{
    private const double Gap = 6;
    private const double EdgePadding = 8;

    public static AppHoverOverlayFrame GetOverlayFrame(
        PopupFlowDirection direction,
        double iconLeft,
        double iconTop,
        double iconWidth,
        double iconHeight,
        double overlayWidth,
        double overlayHeight,
        double layerWidth,
        double layerHeight)
    {
        double left;
        if (direction == PopupFlowDirection.Left)
        {
            left = iconLeft - overlayWidth - Gap;
        }
        else
        {
            left = iconLeft + iconWidth + Gap;
        }

        var top = iconTop + ((iconHeight - overlayHeight) / 2);

        left = Clamp(left, EdgePadding, Math.Max(EdgePadding, layerWidth - overlayWidth - EdgePadding));
        top = Clamp(top, EdgePadding, Math.Max(EdgePadding, layerHeight - overlayHeight - EdgePadding));

        return new AppHoverOverlayFrame(left, top);
    }

    public static AppHoverOverlayFrame GetOverlayFrame(
        double iconLeft,
        double iconTop,
        double iconWidth,
        double iconHeight,
        double overlayWidth,
        double overlayHeight,
        double layerWidth,
        double layerHeight)
    {
        return GetOverlayFrame(
            PopupFlowDirection.Right,
            iconLeft, iconTop, iconWidth, iconHeight,
            overlayWidth, overlayHeight, layerWidth, layerHeight);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
