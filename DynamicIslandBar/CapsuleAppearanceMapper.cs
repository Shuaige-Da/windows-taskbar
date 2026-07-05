using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DynamicIslandBar;

public static class CapsuleAppearanceMapper
{
    public const double TopIslandDefaultWidth = 760d;

    public static LinearGradientBrush BuildBackgroundBrush(int opacityPercent)
    {
        var opacity = Math.Clamp(opacityPercent, 0, 100) / 100.0;
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1),
            Opacity = opacity
        };

        brush.GradientStops.Add(new GradientStop(Color.FromRgb(216, 245, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(26, 51, 70), 0.42));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 0, 0), 1));
        return brush;
    }

    public static LinearGradientBrush BuildPanelBackgroundBrush(int opacityPercent)
    {
        var opacity = Math.Clamp(opacityPercent + 14, 0, 100) / 100.0;
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1),
            Opacity = opacity
        };

        brush.GradientStops.Add(new GradientStop(Color.FromRgb(34, 53, 66), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(18, 28, 38), 0.55));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(6, 9, 14), 1));
        return brush;
    }

    public static DropShadowEffect? BuildShadowEffect(int shadowPercent)
    {
        var percent = Math.Clamp(shadowPercent, 0, 100);
        if (percent <= 0)
        {
            return null;
        }

        var ratio = percent / 100.0;
        return new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 4 + (28 * ratio),
            ShadowDepth = 8 * ratio,
            Opacity = 0.14 + (0.28 * ratio)
        };
    }

    public static DropShadowEffect? BuildPanelShadowEffect(int shadowPercent)
    {
        var percent = Math.Clamp(shadowPercent, 0, 100);
        var ratio = percent / 100.0;
        return new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 12 + (18 * ratio),
            ShadowDepth = 4 + (5 * ratio),
            Opacity = 0.22 + (0.24 * ratio)
        };
    }

    public static SolidColorBrush BuildPanelBorderBrush(int glowIntensityPercent)
    {
        var ratio = Math.Clamp(glowIntensityPercent, 0, 100) / 100.0;
        return new SolidColorBrush(Color.FromArgb(
            (byte)(52 + (78 * ratio)),
            70,
            224,
            255));
    }

    public static LinearGradientBrush BuildGlowBrush(int glowIntensityPercent, Color? accent = null)
    {
        var opacity = Math.Clamp(glowIntensityPercent, 0, 100) / 100.0;
        var primary = accent ?? Color.FromRgb(255, 79, 163);
        var secondary = accent.HasValue ? BoostForGlow(accent.Value) : Color.FromRgb(70, 224, 255);
        var tail = accent.HasValue ? Color.FromArgb(90, accent.Value.R, accent.Value.G, accent.Value.B) : Color.FromRgb(77, 255, 136);

        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0.5),
            EndPoint = new System.Windows.Point(1, 0.5),
            SpreadMethod = GradientSpreadMethod.Repeat,
            Opacity = opacity,
            RelativeTransform = new TranslateTransform(-1, 0)
        };

        brush.GradientStops.Add(new GradientStop(Color.FromArgb(40, primary.R, primary.G, primary.B), 0));
        brush.GradientStops.Add(new GradientStop(primary, 0.12));
        brush.GradientStops.Add(new GradientStop(secondary, 0.22));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(60, secondary.R, secondary.G, secondary.B), 0.32));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(22, tail.R, tail.G, tail.B), 0.62));
        brush.GradientStops.Add(new GradientStop(tail, 0.82));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(35, primary.R, primary.G, primary.B), 1));
        return brush;
    }

    public static double MapGlowThickness(int glowThicknessPercent)
    {
        var ratio = Math.Clamp(glowThicknessPercent, 0, 100) / 100.0;
        return 0.8 + (2.8 * ratio);
    }

    public static TimeSpan MapGlowDuration(int glowSpeedPercent)
    {
        var ratio = Math.Clamp(glowSpeedPercent, 0, 100) / 100.0;
        return TimeSpan.FromSeconds(4.2 - (3.0 * ratio));
    }

    public static double MapCapsuleHeight(double baseHeight, int capsuleThicknessPercent)
    {
        var displayRatio = Math.Clamp(capsuleThicknessPercent, 0, 100) / 100.0;
        var ratio = 0.5 + (displayRatio * 0.5);
        var minimumHeight = baseHeight / 3d;
        return minimumHeight + ((baseHeight - minimumHeight) * ratio);
    }

    public static double MapCapsuleWidth(CapsuleMode mode, double baseWidth, int capsuleLengthPercent)
    {
        if (mode == CapsuleMode.TopIsland)
        {
            return Math.Min(TopIslandDefaultWidth, baseWidth);
        }

        // 侧边模式：baseWidth 是屏幕高度，映射到竖向胶囊长度
        if (mode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            var sideRatio = Math.Clamp(capsuleLengthPercent, 0, 100) / 100.0;
            var sideMin = Math.Min(baseWidth * 2.0 / 3.0, baseWidth);
            return sideMin + ((baseWidth - sideMin) * sideRatio);
        }

        var ratio = Math.Clamp(capsuleLengthPercent, 0, 100) / 100.0;
        var minWidth = TopIslandDefaultWidth;
        minWidth = Math.Min(minWidth, baseWidth);
        return minWidth + ((baseWidth - minWidth) * ratio);
    }

    private static Color BoostForGlow(Color color)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(color.R + 42, 0, 255),
            (byte)Math.Clamp(color.G + 42, 0, 255),
            (byte)Math.Clamp(color.B + 42, 0, 255));
    }
}
