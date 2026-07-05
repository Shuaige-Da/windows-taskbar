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
        Assert.Equal(1920, bottom.CapsuleWidth);
        Assert.Equal(80, bottom.CapsuleHeight);
        Assert.Equal(72, top.CapsuleHeight);
        Assert.Equal(8, bottom.VisibleAppSlots);
        Assert.True(bottom.VisibleAppSlots > top.VisibleAppSlots);
        Assert.Equal(PopupFlowDirection.Up, bottom.PopupDirection);
        Assert.Equal(PopupFlowDirection.Down, top.PopupDirection);
    }

    [Fact]
    public void GetMetrics_LeavesRoomForCenterCardInTopIslandMode()
    {
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

        Assert.Equal(3, top.VisibleAppSlots);
    }

    [Fact]
    public void ResolveDropMode_SnapsToTopWhenCloseToTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920, screenHeight: 1080,
            leftAfterDrag: 900, topAfterDrag: 20,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(CapsuleMode.TopIsland, mode);
    }

    [Fact]
    public void GetWindowFrame_CentersBottomModeInsidePrimaryScreen()
    {
        var metrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 2048, 1152);

        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.BottomTaskbar,
            metrics,
            screenWidth: 2048,
            screenHeight: 1152);

        Assert.Equal(2088, frame.Width);
        Assert.Equal(420, frame.Height);
        Assert.Equal(-20, frame.Left);
        Assert.Equal(902, frame.Top);
    }

    [Fact]
    public void GetMetrics_UsesTopPopupDefaultsForSideDockModes()
    {
        var left = CapsuleLayoutManager.GetMetrics(CapsuleMode.LeftDock, 1920, 1080);
        var right = CapsuleLayoutManager.GetMetrics(CapsuleMode.RightDock, 1920, 1080);
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

        // Side dock modes use screen height as the base for vertical capsule length
        Assert.Equal(1080, left.CapsuleWidth);
        Assert.Equal(top.CapsuleHeight, left.CapsuleHeight);
        Assert.Equal(PopupFlowDirection.Right, left.PopupDirection);
        Assert.Equal(PopupFlowDirection.Left, right.PopupDirection);
    }
}
