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
                CapsuleBackground: "#B814302A",
                PanelBackground: "#D317211E",
                AccentColor: "#4CD964",
                BorderBrush: "#554CD964",
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
                CapsuleBackground: "#A6183144",
                PanelBackground: "#D8182634",
                AccentColor: "#4CD964",
                BorderBrush: "#78D8F3FF",
                BackgroundImagePath: backgroundImagePath,
                BackgroundImageOpacity: backgroundImageOpacity)
        };
    }
}
