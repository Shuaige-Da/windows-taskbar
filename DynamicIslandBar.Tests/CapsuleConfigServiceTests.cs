using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleConfigServiceTests
{
    [Fact]
    public void Deserialize_ReturnsDefaults_WhenJsonIsEmpty()
    {
        var config = CapsuleConfigSerializer.Deserialize("{}");

        Assert.Equal(CapsuleMode.BottomTaskbar, config.Mode);
        Assert.Equal(CapsuleThemePreset.ClassicDark, config.ThemePreset);
        Assert.Empty(config.FavoriteApps);
        Assert.Empty(config.HiddenApps);
        Assert.Empty(config.KnownLaunchPaths);
        Assert.Equal(100, config.CapsuleThicknessPercent);
        Assert.Equal(100, config.CapsuleLengthPercent);
        Assert.Equal(58, config.CenterCardWidthPercent);
        Assert.Null(config.CenterCardAppId);
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
}
