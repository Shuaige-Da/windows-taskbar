using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CenterCardLayoutPolicyTests
{
    [Theory]
    [InlineData(CapsuleMode.BottomTaskbar, 1920, 58, 590.6)]
    [InlineData(CapsuleMode.BottomTaskbar, 760, 58, 233.8)]
    [InlineData(CapsuleMode.TopIsland, 760, 58, 233.8)]
    [InlineData(CapsuleMode.TopIsland, 760, 100, 304)]
    public void MapWidth_ScalesWithCapsuleWidthUsingStoredRatio(
        CapsuleMode mode,
        double capsuleWidth,
        int percent,
        double expected)
    {
        Assert.Equal(expected, CenterCardLayoutPolicy.MapWidth(mode, capsuleWidth, percent), precision: 1);
    }

    [Theory]
    [InlineData(CapsuleMode.TopIsland, 760, 58, 233.8)]
    [InlineData(CapsuleMode.LeftDock, 760, 58, 233.8)]
    [InlineData(CapsuleMode.RightDock, 760, 58, 233.8)]
    public void MapWidth_ScalesWithTopRatioForTopAndSideModes(
        CapsuleMode mode,
        double capsuleWidth,
        int percent,
        double expected)
    {
        Assert.Equal(expected, CenterCardLayoutPolicy.MapWidth(mode, capsuleWidth, percent), precision: 1);
    }

    [Theory]
    [InlineData(CapsuleMode.BottomTaskbar, 1920, 590.6, 58)]
    [InlineData(CapsuleMode.BottomTaskbar, 760, 233.8, 58)]
    [InlineData(CapsuleMode.TopIsland, 760, 233.8, 58)]
    public void MapWidthPercent_InvertsMapWidth(CapsuleMode mode, double capsuleWidth, double width, int expected)
    {
        Assert.Equal(expected, CenterCardLayoutPolicy.MapWidthPercent(mode, capsuleWidth, width));
    }

    [Theory]
    [InlineData(CapsuleMode.BottomTaskbar, 760, 100, 250, 198)]
    [InlineData(CapsuleMode.TopIsland, 760, 100, 250, 198)]
    [InlineData(CapsuleMode.TopIsland, 760, 58, 250, 198)]
    public void MapWidth_ClampsToAvailableCenterSlot(
        CapsuleMode mode,
        double capsuleWidth,
        int percent,
        double availableCenterSlotWidth,
        double expected)
    {
        Assert.Equal(
            expected,
            CenterCardLayoutPolicy.MapWidth(mode, capsuleWidth, percent, availableCenterSlotWidth),
            precision: 1);
    }

    [Theory]
    [InlineData(520, CenterCardTransportDensity.Full)]
    [InlineData(360, CenterCardTransportDensity.Compact)]
    [InlineData(234, CenterCardTransportDensity.Minimal)]
    public void GetTransportDensity_CompactsControlsWhenCenterCardIsNarrow(
        double centerCardWidth,
        CenterCardTransportDensity expected)
    {
        Assert.Equal(expected, CenterCardLayoutPolicy.GetTransportDensity(centerCardWidth));
    }

    [Theory]
    [InlineData(520, 42, true, true)]
    [InlineData(360, 30, true, false)]
    [InlineData(198, 24, false, false)]
    public void GetLyricsLayout_PrioritizesTextSpaceWhenCenterCardIsNarrow(
        double centerCardWidth,
        double expectedHorizontalMargin,
        bool expectedLeftWave,
        bool expectedRightWave)
    {
        var layout = CenterCardLayoutPolicy.GetLyricsLayout(centerCardWidth);

        Assert.Equal(expectedHorizontalMargin, layout.HorizontalMargin);
        Assert.Equal(expectedLeftWave, layout.ShowLeftWave);
        Assert.Equal(expectedRightWave, layout.ShowRightWave);
    }

    [Theory]
    [InlineData(198, false, false)]
    [InlineData(360, true, false)]
    [InlineData(520, true, true)]
    public void GetLyricsLayout_PrioritizesTextSpaceAcrossHorizontalAndVerticalModes(
        double centerCardExtent,
        bool expectedLeadingWave,
        bool expectedTrailingWave)
    {
        var layout = CenterCardLayoutPolicy.GetLyricsLayout(centerCardExtent);

        Assert.Equal(expectedLeadingWave, layout.ShowLeftWave);
        Assert.Equal(expectedTrailingWave, layout.ShowRightWave);
    }

    [Theory]
    [InlineData(233.8, 320, 233.8)]
    [InlineData(304, 320, 304)]
    [InlineData(120, 320, 120)]
    [InlineData(233.8, 110, 110)]
    public void MapSideDockExtent_UsesSameConfiguredLengthUntilAvailableHeightBoundary(
        double mappedTopLength,
        double availableHeight,
        double expected)
    {
        Assert.Equal(
            expected,
            CenterCardLayoutPolicy.MapSideDockExtent(mappedTopLength, availableHeight),
            precision: 1);
    }
}
