using System.Text.Json;

namespace DynamicIslandBar;

public enum CapsuleMode
{
    BottomTaskbar,
    TopIsland
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
}

public static class CapsuleConfigSerializer
{
    public static CapsuleConfig Deserialize(string json)
    {
        var store = JsonSerializer.Deserialize<CapsuleConfigStore>(json) ?? new CapsuleConfigStore();
        return store.ToConfig();
    }

    private sealed class CapsuleConfigStore
    {
        public CapsuleMode Mode { get; set; } = CapsuleMode.BottomTaskbar;
        public CapsuleThemePreset ThemePreset { get; set; } = CapsuleThemePreset.ClassicDark;
        public List<string> FavoriteApps { get; set; } = [];
        public List<string> HiddenApps { get; set; } = [];
        public Dictionary<string, string> KnownLaunchPaths { get; set; } = [];
        public string? BackgroundImagePath { get; set; }
        public double BackgroundImageOpacity { get; set; }
        public string? BackgroundImageStretchMode { get; set; }

        public CapsuleConfig ToConfig()
        {
            var config = new CapsuleConfig
            {
                Mode = Mode,
                ThemePreset = ThemePreset,
                BackgroundImagePath = BackgroundImagePath,
                BackgroundImageOpacity = BackgroundImageOpacity,
                BackgroundImageStretchMode = BackgroundImageStretchMode
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

    public static void SetKnownLaunchPath(CapsuleConfig config, string appId, string path)
    {
        config.KnownLaunchPaths[appId] = path;
    }
}
