using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleConfigServiceTests
{
    [Fact]
    public void Serialize_RoundTripsStartupDisplayMode()
    {
        var config = new CapsuleConfig
        {
            StartupDisplayMode = StartupDisplayMode.CapsuleOnly
        };

        var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(config));

        Assert.Equal(StartupDisplayMode.CapsuleOnly, restored.StartupDisplayMode);
    }

    [Fact]
    public void ReplaceWith_UpdatesExistingConfigInstanceAndCollections()
    {
        var target = new CapsuleConfig();
        target.FavoriteApps.Add("old-app");
        var replacement = new CapsuleConfig
        {
            ThemePreset = CapsuleThemePreset.GlassGreen,
            StartupDisplayMode = StartupDisplayMode.CapsuleOnly,
            GlassOpacityPercent = 44,
            LyricLanguage = LyricLanguage.Traditional
        };
        replacement.FavoriteApps.Add("new-app");
        replacement.HiddenApps.Add("hidden-app");
        replacement.KnownLaunchPaths["new-app"] = @"C:\Apps\New.exe";
        CapsuleConfigMutator.SetPartVisibility(replacement, CapsuleVisualPart.Lyrics, false);

        CapsuleConfigMutator.ReplaceWith(target, replacement);

        Assert.Equal(CapsuleThemePreset.GlassGreen, target.ThemePreset);
        Assert.Equal(StartupDisplayMode.CapsuleOnly, target.StartupDisplayMode);
        Assert.Equal(44, target.GlassOpacityPercent);
        Assert.Equal(LyricLanguage.Traditional, target.LyricLanguage);
        Assert.Equal(["new-app"], target.FavoriteApps);
        Assert.Equal(["hidden-app"], target.HiddenApps);
        Assert.Equal(@"C:\Apps\New.exe", target.KnownLaunchPaths["new-app"]);
        Assert.False(target.Presentation.Lyrics.IsVisible);
    }

    [Fact]
    public void Save_CreatesValidatedBackupAndLoadRecoversCorruptPrimary()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var configPath = Path.Combine(directory, "capsule-config.json");
            var backupPath = configPath + ".bak";
            Assert.True(CapsuleConfigService.TrySave(
                new CapsuleConfig { ThemePreset = CapsuleThemePreset.ClassicDark },
                configPath,
                backupPath));
            Assert.True(CapsuleConfigService.TrySave(
                new CapsuleConfig { ThemePreset = CapsuleThemePreset.GlassGreen },
                configPath,
                backupPath));

            File.WriteAllText(configPath, "{ damaged json");
            var recovered = CapsuleConfigService.Load(configPath, backupPath);

            Assert.Equal(CapsuleThemePreset.ClassicDark, recovered.ThemePreset);
            Assert.Equal(
                CapsuleThemePreset.ClassicDark,
                CapsuleConfigSerializer.Deserialize(File.ReadAllText(configPath)).ThemePreset);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExportAndImport_RoundTripValidatedConfiguration()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var exportPath = Path.Combine(directory, "export.json");
            var config = new CapsuleConfig
            {
                ThemePreset = CapsuleThemePreset.SoftLight,
                CenterCardWidthPercent = 73
            };

            Assert.True(CapsuleConfigService.TryExport(config, exportPath, out var exportError), exportError);
            Assert.True(CapsuleConfigService.TryImport(exportPath, out var imported, out var importError), importError);
            Assert.Equal(CapsuleThemePreset.SoftLight, imported!.ThemePreset);
            Assert.Equal(73, imported.CenterCardWidthPercent);

            var invalidPath = Path.Combine(directory, "invalid.json");
            File.WriteAllText(invalidPath, "not-json");
            Assert.False(CapsuleConfigService.TryImport(invalidPath, out _, out var invalidError));
            Assert.Contains("无效", invalidError);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"DynamicIslandBar-Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public void Deserialize_ReturnsDefaults_WhenJsonIsEmpty()
    {
        var config = CapsuleConfigSerializer.Deserialize("{}");

        Assert.Equal(CapsuleMode.BottomTaskbar, config.Mode);
        Assert.Equal(CapsuleThemePreset.ClassicDark, config.ThemePreset);
        Assert.Equal(StartupDisplayMode.CapsuleAndControlCenter, config.StartupDisplayMode);
        Assert.Empty(config.FavoriteApps);
        Assert.Empty(config.HiddenApps);
        Assert.Empty(config.KnownLaunchPaths);
        Assert.Equal(100, config.CapsuleThicknessPercent);
        Assert.Equal(100, config.CapsuleLengthPercent);
        Assert.Equal(58, config.CenterCardWidthPercent);
        Assert.Null(config.CenterCardAppId);
        foreach (var part in Enum.GetValues<CapsuleVisualPart>())
        {
            Assert.True(config.Presentation.Get(part).IsVisible);
            Assert.Equal(100, config.Presentation.Get(part).OpacityPercent);
        }
    }

    [Fact]
    public void SetKnownLaunchPath_StoresNormalizedAppIdAndPath()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetKnownLaunchPath(
            config,
            @"c:\apps\wechat.exe",
            @"C:\Apps\WeChat.exe");

        Assert.Equal(
            @"C:\Apps\WeChat.exe",
            config.KnownLaunchPaths[@"c:\apps\wechat.exe"]);
    }

    [Fact]
    public void SetFavorite_AddsThenRemovesAppId()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetFavorite(config, "wechat", true);
        CapsuleConfigMutator.SetFavorite(config, "wechat", false);

        Assert.DoesNotContain("wechat", config.FavoriteApps);
    }

    [Fact]
    public void Serialize_RoundTripsCenterCardSettings()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetCenterCardApp(config, "cloudmusic");
        CapsuleConfigMutator.SetCenterCardWidthPercent(config, 130);

        var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(config));

        Assert.Equal("cloudmusic", restored.CenterCardAppId);
        Assert.Equal(100, restored.CenterCardWidthPercent);
    }

    [Fact]
    public void Serialize_RoundTripsFloatingPlacement()
    {
        var config = new CapsuleConfig
        {
            Mode = CapsuleMode.Floating,
            FloatingLeft = 312.5,
            FloatingTop = 228.25,
            LastBottomCapsuleWidth = 1280,
            LastBottomCapsuleHeight = 80
        };

        var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(config));

        Assert.Equal(CapsuleMode.Floating, restored.Mode);
        Assert.Equal(312.5, restored.FloatingLeft, 2);
        Assert.Equal(228.25, restored.FloatingTop, 2);
        Assert.Equal(1280, restored.LastBottomCapsuleWidth, 2);
        Assert.Equal(80, restored.LastBottomCapsuleHeight, 2);
    }

    [Fact]
    public void Deserialize_PreservesLegacyNumericModeValues()
    {
        var config = CapsuleConfigSerializer.Deserialize("""
            {
              "Mode": 0
            }
            """);

        Assert.Equal(CapsuleMode.BottomTaskbar, config.Mode);
    }

    [Fact]
    public void Serialize_RoundTripsPresentationSettingsAndClampsOpacity()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetPartVisibility(config, CapsuleVisualPart.Lyrics, false);
        CapsuleConfigMutator.SetPartOpacityPercent(config, CapsuleVisualPart.Dock, 140);
        CapsuleConfigMutator.SetPartOpacityPercent(config, CapsuleVisualPart.System, -10);

        var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(config));

        Assert.False(restored.Presentation.Lyrics.IsVisible);
        Assert.Equal(100, restored.Presentation.Dock.OpacityPercent);
        Assert.Equal(0, restored.Presentation.System.OpacityPercent);
    }

    [Fact]
    public void Presentation_CenterCardDefaultsForLegacyJsonAndRoundTripsPreference()
    {
        var legacy = CapsuleConfigSerializer.Deserialize("""
            {
              "Presentation": {
                "Lyrics": { "IsVisible": true, "OpacityPercent": 80 }
              }
            }
            """);

        Assert.True(legacy.Presentation.CenterCard.IsVisible);
        Assert.Equal(100, legacy.Presentation.CenterCard.OpacityPercent);

        CapsuleConfigMutator.SetPartVisibility(legacy, CapsuleVisualPart.CenterCard, false);
        CapsuleConfigMutator.SetPartOpacityPercent(legacy, CapsuleVisualPart.CenterCard, 63);
        var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(legacy));

        Assert.False(restored.Presentation.CenterCard.IsVisible);
        Assert.Equal(63, restored.Presentation.CenterCard.OpacityPercent);
    }
}
