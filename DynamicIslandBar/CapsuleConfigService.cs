using System.IO;
using System.Text.Json;

namespace DynamicIslandBar;

public enum CapsuleMode
{
    BottomTaskbar,
    TopIsland,
    LeftDock,
    RightDock
}

public enum CapsuleThemePreset
{
    ClassicDark,
    GlassGreen,
    SoftLight
}

public sealed class CapsuleConfig
{
    public CapsuleMode Mode { get; set; } = CapsuleMode.BottomTaskbar;
    public CapsuleThemePreset ThemePreset { get; set; } = CapsuleThemePreset.ClassicDark;
    public HashSet<string> FavoriteApps { get; } = [];
    public HashSet<string> HiddenApps { get; } = [];
    public Dictionary<string, string> KnownLaunchPaths { get; } = [];
    public string? BackgroundImagePath { get; set; }
    public double BackgroundImageOpacity { get; set; }
    public string? BackgroundImageStretchMode { get; set; }
    public int GlassOpacityPercent { get; set; } = 72;
    public int ShadowPercent { get; set; } = 0;
    public int GlowIntensityPercent { get; set; } = 82;
    public int GlowThicknessPercent { get; set; } = 42;
    public int GlowSpeedPercent { get; set; } = 58;
    public int CapsuleThicknessPercent { get; set; } = 100;
    public int CapsuleLengthPercent { get; set; } = 100;
    public int CenterCardWidthPercent { get; set; } = 58;
    public string? CenterCardAppId { get; set; }
    public LyricLanguage LyricLanguage { get; set; } = LyricLanguage.Simplified;
}

public static class CapsuleConfigSerializer
{
    public static CapsuleConfig Deserialize(string json)
    {
        var store = JsonSerializer.Deserialize<CapsuleConfigStore>(json) ?? new CapsuleConfigStore();
        return store.ToConfig();
    }

    public static string Serialize(CapsuleConfig config)
    {
        var store = CapsuleConfigStore.FromConfig(config);
        return JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
    }
}

public static class CapsuleConfigMutator
{
    public static void SetFavorite(CapsuleConfig config, string appId, bool isFavorite)
    {
        if (isFavorite)
        {
            config.FavoriteApps.Add(appId);
            return;
        }

        config.FavoriteApps.Remove(appId);
    }

    public static void SetHidden(CapsuleConfig config, string appId, bool isHidden)
    {
        if (isHidden)
        {
            config.HiddenApps.Add(appId);
            return;
        }

        config.HiddenApps.Remove(appId);
    }

    public static void SetKnownLaunchPath(CapsuleConfig config, string appId, string path)
    {
        config.KnownLaunchPaths[appId] = path;
    }

    public static void SetMode(CapsuleConfig config, CapsuleMode mode)
    {
        config.Mode = mode;
    }

    public static void SetThemePreset(CapsuleConfig config, CapsuleThemePreset themePreset)
    {
        config.ThemePreset = themePreset;
    }

    public static void SetGlassOpacityPercent(CapsuleConfig config, int percent)
    {
        config.GlassOpacityPercent = ClampPercent(percent);
    }

    public static void SetShadowPercent(CapsuleConfig config, int percent)
    {
        config.ShadowPercent = ClampPercent(percent);
    }

    public static void SetGlowIntensityPercent(CapsuleConfig config, int percent)
    {
        config.GlowIntensityPercent = ClampPercent(percent);
    }

    public static void SetGlowThicknessPercent(CapsuleConfig config, int percent)
    {
        config.GlowThicknessPercent = ClampPercent(percent);
    }

    public static void SetGlowSpeedPercent(CapsuleConfig config, int percent)
    {
        config.GlowSpeedPercent = ClampPercent(percent);
    }

    public static void SetCapsuleThicknessPercent(CapsuleConfig config, int percent)
    {
        config.CapsuleThicknessPercent = ClampPercent(percent);
    }

    public static void SetCapsuleLengthPercent(CapsuleConfig config, int percent)
    {
        config.CapsuleLengthPercent = ClampPercent(percent);
    }

    public static void SetCenterCardWidthPercent(CapsuleConfig config, int percent)
    {
        config.CenterCardWidthPercent = ClampPercent(percent);
    }

    public static void SetCenterCardApp(CapsuleConfig config, string? appId)
    {
        config.CenterCardAppId = string.IsNullOrWhiteSpace(appId) ? null : appId;
    }

    public static void SetLyricLanguage(CapsuleConfig config, LyricLanguage language)
    {
        config.LyricLanguage = language;
    }

    private static int ClampPercent(int percent)
    {
        return Math.Clamp(percent, 0, 100);
    }
}

public static class CapsuleConfigService
{
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIslandBar",
        "capsule-config.json");

    public static CapsuleConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return new CapsuleConfig();
            }

            return CapsuleConfigSerializer.Deserialize(File.ReadAllText(ConfigFilePath));
        }
        catch
        {
            return new CapsuleConfig();
        }
    }

    public static void Save(CapsuleConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
            File.WriteAllText(ConfigFilePath, CapsuleConfigSerializer.Serialize(config));
        }
        catch
        {
        }
    }
}

internal sealed class CapsuleConfigStore
{
    public CapsuleMode Mode { get; set; } = CapsuleMode.BottomTaskbar;
    public CapsuleThemePreset ThemePreset { get; set; } = CapsuleThemePreset.ClassicDark;
    public List<string> FavoriteApps { get; set; } = [];
    public List<string> HiddenApps { get; set; } = [];
    public Dictionary<string, string> KnownLaunchPaths { get; set; } = [];
    public string? BackgroundImagePath { get; set; }
    public double BackgroundImageOpacity { get; set; }
    public string? BackgroundImageStretchMode { get; set; }
    public int GlassOpacityPercent { get; set; } = 72;
    public int ShadowPercent { get; set; } = 0;
    public int GlowIntensityPercent { get; set; } = 82;
    public int GlowThicknessPercent { get; set; } = 42;
    public int GlowSpeedPercent { get; set; } = 58;
    public int CapsuleThicknessPercent { get; set; } = 100;
    public int CapsuleLengthPercent { get; set; } = 100;
    public int CenterCardWidthPercent { get; set; } = 58;
    public string? CenterCardAppId { get; set; }
    public LyricLanguage LyricLanguage { get; set; } = LyricLanguage.Simplified;

    public CapsuleConfig ToConfig()
    {
        var config = new CapsuleConfig
        {
            Mode = Mode,
            ThemePreset = ThemePreset,
            BackgroundImagePath = BackgroundImagePath,
            BackgroundImageOpacity = BackgroundImageOpacity,
            BackgroundImageStretchMode = BackgroundImageStretchMode,
            GlassOpacityPercent = ClampPercent(GlassOpacityPercent),
            ShadowPercent = ClampPercent(ShadowPercent),
            GlowIntensityPercent = ClampPercent(GlowIntensityPercent),
            GlowThicknessPercent = ClampPercent(GlowThicknessPercent),
            GlowSpeedPercent = ClampPercent(GlowSpeedPercent),
            CapsuleThicknessPercent = ClampPercent(CapsuleThicknessPercent),
            CapsuleLengthPercent = ClampPercent(CapsuleLengthPercent),
            CenterCardWidthPercent = ClampPercent(CenterCardWidthPercent),
            CenterCardAppId = CenterCardAppId,
            LyricLanguage = LyricLanguage
        };

        foreach (var appId in FavoriteApps)
        {
            config.FavoriteApps.Add(appId);
        }

        foreach (var appId in HiddenApps)
        {
            config.HiddenApps.Add(appId);
        }

        foreach (var pair in KnownLaunchPaths)
        {
            config.KnownLaunchPaths[pair.Key] = pair.Value;
        }

        return config;
    }

    public static CapsuleConfigStore FromConfig(CapsuleConfig config)
    {
        return new CapsuleConfigStore
        {
            Mode = config.Mode,
            ThemePreset = config.ThemePreset,
            FavoriteApps = [.. config.FavoriteApps.Order(StringComparer.OrdinalIgnoreCase)],
            HiddenApps = [.. config.HiddenApps.Order(StringComparer.OrdinalIgnoreCase)],
            KnownLaunchPaths = new Dictionary<string, string>(config.KnownLaunchPaths),
            BackgroundImagePath = config.BackgroundImagePath,
            BackgroundImageOpacity = config.BackgroundImageOpacity,
            BackgroundImageStretchMode = config.BackgroundImageStretchMode,
            GlassOpacityPercent = ClampPercent(config.GlassOpacityPercent),
            ShadowPercent = ClampPercent(config.ShadowPercent),
            GlowIntensityPercent = ClampPercent(config.GlowIntensityPercent),
            GlowThicknessPercent = ClampPercent(config.GlowThicknessPercent),
            GlowSpeedPercent = ClampPercent(config.GlowSpeedPercent),
            CapsuleThicknessPercent = ClampPercent(config.CapsuleThicknessPercent),
            CapsuleLengthPercent = ClampPercent(config.CapsuleLengthPercent),
            CenterCardWidthPercent = ClampPercent(config.CenterCardWidthPercent),
            CenterCardAppId = config.CenterCardAppId,
            LyricLanguage = config.LyricLanguage
        };
    }

    private static int ClampPercent(int percent)
    {
        return Math.Clamp(percent, 0, 100);
    }
}
