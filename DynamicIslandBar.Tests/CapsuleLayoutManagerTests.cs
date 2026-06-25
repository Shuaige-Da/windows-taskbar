using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleLayoutManagerTests
{
    [Fact]
    public void GetMetrics_ReturnsWiderCapacityForBottomMode()
    {
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

        Assert.True(bottom.CapsuleWidth > top.CapsuleWidth);
        Assert.True(bottom.VisibleAppSlots > top.VisibleAppSlots);
        Assert.Equal(PopupFlowDirection.Up, bottom.PopupDirection);
        Assert.Equal(PopupFlowDirection.Down, top.PopupDirection);
    }

    [Fact]
    public void ResolveDropMode_SnapsToTopWhenCloseToTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenHeight: 1080,
            topAfterDrag: 20,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(CapsuleMode.TopIsland, mode);
    }
}
