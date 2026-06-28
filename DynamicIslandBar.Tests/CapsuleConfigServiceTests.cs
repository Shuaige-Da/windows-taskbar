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
}
