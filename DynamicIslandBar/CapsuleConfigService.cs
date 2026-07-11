using System.IO;
using System.Text.Json;

namespace DynamicIslandBar;

public enum CapsuleMode
{
    BottomTaskbar = 0,
    TopIsland = 1,
    LeftDock = 2,
    RightDock = 3,
    Floating = 4
}

public enum CapsuleThemePreset
{
    ClassicDark,
    GlassGreen,
    SoftLight
}

public enum StartupDisplayMode
{
    CapsuleAndControlCenter,
    CapsuleOnly
}

public sealed class CapsuleConfig
{
    public CapsuleMode Mode { get; set; } = CapsuleMode.BottomTaskbar;
    public double FloatingLeft { get; set; }
    public double FloatingTop { get; set; }
    public double LastBottomCapsuleWidth { get; set; }
    public double LastBottomCapsuleHeight { get; set; } = 80;
    public CapsuleThemePreset ThemePreset { get; set; } = CapsuleThemePreset.ClassicDark;
    public StartupDisplayMode StartupDisplayMode { get; set; } =
        DynamicIslandBar.StartupDisplayMode.CapsuleAndControlCenter;
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
    public int TopDockCapsuleLengthPercent { get; set; } = 0;
    public int CenterCardWidthPercent { get; set; } = 58;
    public string? CenterCardAppId { get; set; }
    public LyricLanguage LyricLanguage { get; set; } = LyricLanguage.Simplified;
    public CapsulePresentationConfig Presentation { get; set; } = new();
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
    public static void ReplaceWith(CapsuleConfig target, CapsuleConfig source)
    {
        var normalized = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(source));
        target.Mode = normalized.Mode;
        target.FloatingLeft = normalized.FloatingLeft;
        target.FloatingTop = normalized.FloatingTop;
        target.LastBottomCapsuleWidth = normalized.LastBottomCapsuleWidth;
        target.LastBottomCapsuleHeight = normalized.LastBottomCapsuleHeight;
        target.ThemePreset = normalized.ThemePreset;
        target.StartupDisplayMode = normalized.StartupDisplayMode;
        target.BackgroundImagePath = normalized.BackgroundImagePath;
        target.BackgroundImageOpacity = normalized.BackgroundImageOpacity;
        target.BackgroundImageStretchMode = normalized.BackgroundImageStretchMode;
        target.GlassOpacityPercent = normalized.GlassOpacityPercent;
        target.ShadowPercent = normalized.ShadowPercent;
        target.GlowIntensityPercent = normalized.GlowIntensityPercent;
        target.GlowThicknessPercent = normalized.GlowThicknessPercent;
        target.GlowSpeedPercent = normalized.GlowSpeedPercent;
        target.CapsuleThicknessPercent = normalized.CapsuleThicknessPercent;
        target.CapsuleLengthPercent = normalized.CapsuleLengthPercent;
        target.TopDockCapsuleLengthPercent = normalized.TopDockCapsuleLengthPercent;
        target.CenterCardWidthPercent = normalized.CenterCardWidthPercent;
        target.CenterCardAppId = normalized.CenterCardAppId;
        target.LyricLanguage = normalized.LyricLanguage;
        target.Presentation = normalized.Presentation.CloneNormalized();

        target.FavoriteApps.Clear();
        target.FavoriteApps.UnionWith(normalized.FavoriteApps);
        target.HiddenApps.Clear();
        target.HiddenApps.UnionWith(normalized.HiddenApps);
        target.KnownLaunchPaths.Clear();
        foreach (var pair in normalized.KnownLaunchPaths)
        {
            target.KnownLaunchPaths[pair.Key] = pair.Value;
        }
    }

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

    public static void SetStartupDisplayMode(CapsuleConfig config, StartupDisplayMode mode)
    {
        config.StartupDisplayMode = Enum.IsDefined(mode)
            ? mode
            : StartupDisplayMode.CapsuleAndControlCenter;
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

    public static void SetTopDockCapsuleLengthPercent(CapsuleConfig config, int percent)
    {
        config.TopDockCapsuleLengthPercent = ClampPercent(percent);
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

    public static void SetPartVisibility(CapsuleConfig config, CapsuleVisualPart part, bool isVisible)
    {
        config.Presentation ??= new CapsulePresentationConfig();
        config.Presentation.Get(part).IsVisible = isVisible;
    }

    public static void SetPartOpacityPercent(CapsuleConfig config, CapsuleVisualPart part, int percent)
    {
        config.Presentation ??= new CapsulePresentationConfig();
        config.Presentation.Get(part).OpacityPercent = ClampPercent(percent);
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
    private static readonly string BackupFilePath = ConfigFilePath + ".bak";

    public static CapsuleConfig Load()
    {
        if (TryReadConfig(ConfigFilePath, out var config))
        {
            return config!;
        }

        if (TryReadConfig(BackupFilePath, out config))
        {
            TryRestoreBackup();
            return config!;
        }

        return new CapsuleConfig();
    }

    public static void Save(CapsuleConfig config)
    {
        TrySave(config, ConfigFilePath, BackupFilePath);
    }

    public static bool TryImport(string sourcePath, out CapsuleConfig? config, out string errorMessage)
    {
        config = null;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            errorMessage = "选择的配置文件不存在。";
            return false;
        }

        if (!TryReadConfig(sourcePath, out config))
        {
            errorMessage = "配置文件格式无效或内容已损坏。";
            return false;
        }

        return true;
    }

    public static bool TryExport(CapsuleConfig config, string destinationPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        var tempPath = destinationPath + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(tempPath, CapsuleConfigSerializer.Serialize(config));
            File.Move(tempPath, destinationPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            errorMessage = $"导出配置失败：{ex.Message}";
            return false;
        }
    }

    internal static bool TrySave(
        CapsuleConfig config,
        string configPath,
        string backupPath)
    {
        var tempPath = configPath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            File.WriteAllText(tempPath, CapsuleConfigSerializer.Serialize(config));
            if (TryReadConfig(configPath, out _))
            {
                File.Copy(configPath, backupPath, overwrite: true);
            }
            File.Move(tempPath, configPath, overwrite: true);
            return true;
        }
        catch
        {
            TryDelete(tempPath);
            return false;
        }
    }

    internal static CapsuleConfig Load(string configPath, string backupPath)
    {
        if (TryReadConfig(configPath, out var config))
        {
            return config!;
        }

        if (TryReadConfig(backupPath, out config))
        {
            try
            {
                File.Copy(backupPath, configPath, overwrite: true);
            }
            catch
            {
            }
            return config!;
        }

        return new CapsuleConfig();
    }

    private static bool TryReadConfig(string path, out CapsuleConfig? config)
    {
        config = null;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }
            config = CapsuleConfigSerializer.Deserialize(File.ReadAllText(path));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryRestoreBackup()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath)!);
            File.Copy(BackupFilePath, ConfigFilePath, overwrite: true);
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

internal sealed class CapsuleConfigStore
{
    public CapsuleMode Mode { get; set; } = CapsuleMode.BottomTaskbar;
    public double FloatingLeft { get; set; }
    public double FloatingTop { get; set; }
    public double LastBottomCapsuleWidth { get; set; }
    public double LastBottomCapsuleHeight { get; set; } = 80;
    public CapsuleThemePreset ThemePreset { get; set; } = CapsuleThemePreset.ClassicDark;
    public StartupDisplayMode StartupDisplayMode { get; set; } =
        DynamicIslandBar.StartupDisplayMode.CapsuleAndControlCenter;
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
    public int TopDockCapsuleLengthPercent { get; set; } = 0;
    public int CenterCardWidthPercent { get; set; } = 58;
    public string? CenterCardAppId { get; set; }
    public LyricLanguage LyricLanguage { get; set; } = LyricLanguage.Simplified;
    public CapsulePresentationConfig? Presentation { get; set; }

    public CapsuleConfig ToConfig()
    {
        var config = new CapsuleConfig
        {
            Mode = Mode,
            FloatingLeft = FloatingLeft,
            FloatingTop = FloatingTop,
            LastBottomCapsuleWidth = LastBottomCapsuleWidth,
            LastBottomCapsuleHeight = LastBottomCapsuleHeight,
            ThemePreset = ThemePreset,
            StartupDisplayMode = Enum.IsDefined(StartupDisplayMode)
                ? StartupDisplayMode
                : DynamicIslandBar.StartupDisplayMode.CapsuleAndControlCenter,
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
            TopDockCapsuleLengthPercent = ClampPercent(TopDockCapsuleLengthPercent),
            CenterCardWidthPercent = ClampPercent(CenterCardWidthPercent),
            CenterCardAppId = CenterCardAppId,
            LyricLanguage = LyricLanguage,
            Presentation = (Presentation ?? new CapsulePresentationConfig()).CloneNormalized()
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
            FloatingLeft = config.FloatingLeft,
            FloatingTop = config.FloatingTop,
            LastBottomCapsuleWidth = config.LastBottomCapsuleWidth,
            LastBottomCapsuleHeight = config.LastBottomCapsuleHeight,
            ThemePreset = config.ThemePreset,
            StartupDisplayMode = config.StartupDisplayMode,
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
            TopDockCapsuleLengthPercent = ClampPercent(config.TopDockCapsuleLengthPercent),
            CenterCardWidthPercent = ClampPercent(config.CenterCardWidthPercent),
            CenterCardAppId = config.CenterCardAppId,
            LyricLanguage = config.LyricLanguage,
            Presentation = (config.Presentation ?? new CapsulePresentationConfig()).CloneNormalized()
        };
    }

    private static int ClampPercent(int percent)
    {
        return Math.Clamp(percent, 0, 100);
    }
}
