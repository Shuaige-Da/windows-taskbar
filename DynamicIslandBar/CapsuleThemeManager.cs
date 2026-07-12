namespace DynamicIslandBar;

public sealed record CapsuleTheme(
    CapsuleThemePreset Preset,
    string CapsuleBackground,
    string PanelBackground,
    string AccentColor,
    string BorderBrush,
    string? BackgroundImagePath,
    double BackgroundImageOpacity);

public static class CapsuleThemeManager
{
    public static CapsuleTheme BuildTheme(
        CapsuleThemePreset preset,
        string? backgroundImagePath = null,
        double backgroundImageOpacity = 0)
    {
        backgroundImageOpacity = Math.Clamp(backgroundImageOpacity, 0d, 1d);
        return preset switch
        {
            CapsuleThemePreset.TransparentWhite => new CapsuleTheme(
                preset,
                CapsuleBackground: "#24FFFFFF",
                PanelBackground: "#38FFFFFF",
                AccentColor: "#F4FAFF",
                BorderBrush: "#B8FFFFFF",
                BackgroundImagePath: backgroundImagePath,
                BackgroundImageOpacity: backgroundImageOpacity),
            _ => new CapsuleTheme(
                preset,
                CapsuleBackground: "#A6183144",
                PanelBackground: "#D8182634",
                AccentColor: "#46E0FF",
                BorderBrush: "#78D8F3FF",
                BackgroundImagePath: backgroundImagePath,
                BackgroundImageOpacity: backgroundImageOpacity)
        };
    }
}
