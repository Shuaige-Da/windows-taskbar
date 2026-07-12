using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleThemeManagerTests
{
    [Theory]
    [InlineData(CapsuleThemePreset.ClassicDark)]
    [InlineData(CapsuleThemePreset.TransparentWhite)]
    public void BuildTheme_ReturnsNamedPreset(CapsuleThemePreset preset)
    {
        var theme = CapsuleThemeManager.BuildTheme(preset);

        Assert.Equal(preset, theme.Preset);
        Assert.False(string.IsNullOrWhiteSpace(theme.CapsuleBackground));
        Assert.False(string.IsNullOrWhiteSpace(theme.PanelBackground));
    }

    [Fact]
    public void BuildTheme_PreservesBackgroundImageFields_WhenNotYetUsed()
    {
        var theme = CapsuleThemeManager.BuildTheme(
            CapsuleThemePreset.ClassicDark,
            backgroundImagePath: @"C:\wallpaper.png",
            backgroundImageOpacity: 0.65);

        Assert.Equal(@"C:\wallpaper.png", theme.BackgroundImagePath);
        Assert.Equal(0.65, theme.BackgroundImageOpacity);
    }

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(0.4, 0.4)]
    [InlineData(2, 1)]
    public void BuildTheme_ClampsBackgroundImageOpacity(double input, double expected)
    {
        var theme = CapsuleThemeManager.BuildTheme(
            CapsuleThemePreset.ClassicDark,
            backgroundImageOpacity: input);

        Assert.Equal(expected, theme.BackgroundImageOpacity);
    }

    [Fact]
    public void BuildTheme_ClassicDarkUsesTranslucentGlassBackground()
    {
        var theme = CapsuleThemeManager.BuildTheme(CapsuleThemePreset.ClassicDark);

        var alpha = Convert.ToByte(theme.CapsuleBackground.Substring(1, 2), 16);

        Assert.True(alpha < 0xCC);
    }

    [Fact]
    public void BuildTheme_TransparentWhiteUsesLowAlphaWhiteGlass()
    {
        var theme = CapsuleThemeManager.BuildTheme(CapsuleThemePreset.TransparentWhite);

        Assert.Equal("#24FFFFFF", theme.CapsuleBackground);
        Assert.Equal("#38FFFFFF", theme.PanelBackground);
        Assert.Equal("#B8FFFFFF", theme.BorderBrush);
    }
}
