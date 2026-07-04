using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class DragSnapLogicTests
{
    [Fact]
    public void ResolveDropMode_ReturnsFloatingWhenDroppedAwayFromTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 420,
            topAfterDrag: 820,
            currentMode: CapsuleMode.TopIsland);

        Assert.Equal(CapsuleMode.Floating, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsBottomWhenDroppedNearBottomThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 420,
            topAfterDrag: 1020,
            currentMode: CapsuleMode.TopIsland);

        Assert.Equal(CapsuleMode.BottomTaskbar, mode);
    }

    [Fact]
    public void GetMetrics_UsesSmallerSystemDensityInTopMode()
    {
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);

        Assert.True(top.CapsuleHeight <= bottom.CapsuleHeight);
    }
}
