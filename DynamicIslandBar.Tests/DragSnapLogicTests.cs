using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class DragSnapLogicTests
{
    [Fact]
    public void ResolveDropMode_ReturnsBottomWhenDroppedAwayFromTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenHeight: 1080,
            topAfterDrag: 820,
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
