using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleAppearanceSettingsTests
{
    [Fact]
    public void Serialize_RoundTripsAppearancePercentages()
    {
        var config = new CapsuleConfig
        {
            GlassOpacityPercent = 72,
            ShadowPercent = 18,
            GlowIntensityPercent = 84,
            GlowThicknessPercent = 36,
            GlowSpeedPercent = 64,
            CapsuleThicknessPercent = 58,
            CapsuleLengthPercent = 74
        };

        var json = CapsuleConfigSerializer.Serialize(config);
        var restored = CapsuleConfigSerializer.Deserialize(json);

        Assert.Equal(72, restored.GlassOpacityPercent);
        Assert.Equal(18, restored.ShadowPercent);
        Assert.Equal(84, restored.GlowIntensityPercent);
        Assert.Equal(36, restored.GlowThicknessPercent);
        Assert.Equal(64, restored.GlowSpeedPercent);
        Assert.Equal(58, restored.CapsuleThicknessPercent);
        Assert.Equal(74, restored.CapsuleLengthPercent);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(45, 45)]
    [InlineData(120, 100)]
    public void SetAppearance_ClampsPercentages(int input, int expected)
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetGlassOpacityPercent(config, input);
        CapsuleConfigMutator.SetShadowPercent(config, input);
        CapsuleConfigMutator.SetGlowIntensityPercent(config, input);
        CapsuleConfigMutator.SetGlowThicknessPercent(config, input);
        CapsuleConfigMutator.SetGlowSpeedPercent(config, input);
        CapsuleConfigMutator.SetCapsuleThicknessPercent(config, input);
        CapsuleConfigMutator.SetCapsuleLengthPercent(config, input);

        Assert.Equal(expected, config.GlassOpacityPercent);
        Assert.Equal(expected, config.ShadowPercent);
        Assert.Equal(expected, config.GlowIntensityPercent);
        Assert.Equal(expected, config.GlowThicknessPercent);
        Assert.Equal(expected, config.GlowSpeedPercent);
        Assert.Equal(expected, config.CapsuleThicknessPercent);
        Assert.Equal(expected, config.CapsuleLengthPercent);
    }

    [Fact]
    public void BuildBackgroundBrush_UsesOpacityPercentForLiquidGlass()
    {
        var brush = CapsuleAppearanceMapper.BuildBackgroundBrush(70);

        Assert.Equal(0.70, brush.Opacity, precision: 2);
        Assert.Equal(3, brush.GradientStops.Count);
    }

    [Fact]
    public void BuildPanelBackgroundBrush_UsesReadableGlassSurface()
    {
        var brush = CapsuleAppearanceMapper.BuildPanelBackgroundBrush(58);

        Assert.True(brush.Opacity > 0.58);
        Assert.Equal(3, brush.GradientStops.Count);
    }

    [Theory]
    [InlineData(0, 52)]
    [InlineData(50, 91)]
    [InlineData(100, 130)]
    public void BuildPanelBorderBrush_FollowsGlowIntensity(int percent, byte expectedAlpha)
    {
        var brush = CapsuleAppearanceMapper.BuildPanelBorderBrush(percent);

        Assert.Equal(expectedAlpha, brush.Color.A);
        Assert.Equal(70, brush.Color.R);
        Assert.Equal(224, brush.Color.G);
        Assert.Equal(255, brush.Color.B);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(50, 18, 4, 0.28)]
    [InlineData(100, 32, 8, 0.42)]
    public void BuildShadowEffect_MapsPercentToShadow(int percent, double blur, double depth, double opacity)
    {
        var effect = CapsuleAppearanceMapper.BuildShadowEffect(percent);

        if (percent == 0)
        {
            Assert.Null(effect);
            return;
        }

        Assert.NotNull(effect);
        Assert.Equal(blur, effect!.BlurRadius, precision: 1);
        Assert.Equal(depth, effect.ShadowDepth, precision: 1);
        Assert.Equal(opacity, effect.Opacity, precision: 2);
    }

    [Fact]
    public void BuildShadowEffect_ForSideDockRemovesDirectionalOffsetStrip()
    {
        var effect = CapsuleAppearanceMapper.BuildShadowEffect(CapsuleMode.LeftDock, 100);

        Assert.NotNull(effect);
        Assert.Equal(0, effect!.ShadowDepth, precision: 1);
        Assert.Equal(32, effect.BlurRadius, precision: 1);
    }

    [Fact]
    public void BuildGlowBrush_UsesMarqueeBandAndIntensity()
    {
        var brush = CapsuleAppearanceMapper.BuildGlowBrush(80);

        Assert.Equal(0.80, brush.Opacity, precision: 2);
        Assert.Equal(7, brush.GradientStops.Count);
        Assert.NotNull(brush.RelativeTransform);
    }

    [Theory]
    [InlineData(0, 0.8)]
    [InlineData(50, 2.2)]
    [InlineData(100, 3.6)]
    public void MapGlowThickness_MapsPercentToBorderWidth(int percent, double expected)
    {
        Assert.Equal(expected, CapsuleAppearanceMapper.MapGlowThickness(percent), precision: 1);
    }

    [Theory]
    [InlineData(0, 4.2)]
    [InlineData(50, 2.7)]
    [InlineData(100, 1.2)]
    public void MapGlowDuration_MapsHigherPercentToFasterSpeed(int percent, double expectedSeconds)
    {
        Assert.Equal(expectedSeconds, CapsuleAppearanceMapper.MapGlowDuration(percent).TotalSeconds, precision: 1);
    }

    [Theory]
    [InlineData(80, 0, 53.3333)]
    [InlineData(80, 50, 66.6667)]
    [InlineData(80, 100, 80)]
    public void MapCapsuleHeight_MapsPercentToThickness(double baseHeight, int percent, double expected)
    {
        Assert.Equal(expected, CapsuleAppearanceMapper.MapCapsuleHeight(baseHeight, percent), precision: 1);
    }

    [Theory]
    [InlineData(CapsuleMode.BottomTaskbar, 1920, 0, 760)]
    [InlineData(CapsuleMode.BottomTaskbar, 1920, 50, 1340)]
    [InlineData(CapsuleMode.BottomTaskbar, 1920, 100, 1920)]
    [InlineData(CapsuleMode.TopIsland, 760, 0, 760)]
    [InlineData(CapsuleMode.TopIsland, 760, 50, 760)]
    [InlineData(CapsuleMode.TopIsland, 760, 100, 760)]
    public void MapCapsuleWidth_MapsPercentToLength(CapsuleMode mode, double baseWidth, int percent, double expected)
    {
        Assert.Equal(expected, CapsuleAppearanceMapper.MapCapsuleWidth(mode, baseWidth, percent), precision: 1);
    }
}
