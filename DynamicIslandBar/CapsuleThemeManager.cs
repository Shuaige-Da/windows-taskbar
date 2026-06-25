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
        return preset switch
        {
            CapsuleThemePreset.GlassGreen => new CapsuleTheme(
                preset,
                CapsuleBackground: "#CC142018",
                PanelBackground: "#D91E2B24",
                AccentColor: "#4CD964",
                BorderBrush: "#334CD964",
                BackgroundImagePath: backgroundImagePath,
                BackgroundImageOpacity: backgroundImageOpacity),
            CapsuleThemePreset.SoftLight => new CapsuleTheme(
                preset,
                CapsuleBackground: "#E6F2F2F2",
                PanelBackground: "#F5FFFFFF",
                AccentColor: "#1F8A70",
                BorderBrush: "#221F8A70",
                BackgroundImagePath: backgroundImagePath,
                BackgroundImageOpacity: backgroundImageOpacity),
            _ => new CapsuleTheme(
                preset,
                CapsuleBackground: "#EB141414",
                PanelBackground: "#F01E1E1E",
                AccentColor: "#4CD964",
                BorderBrush: "#22FFFFFF",
                BackgroundImagePath: backgroundImagePath,
                BackgroundImageOpacity: backgroundImageOpacity)
        };
    }
}
