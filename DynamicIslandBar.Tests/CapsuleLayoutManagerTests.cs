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
    public void GetMetrics_UsesVerticalCapacityForSideDockModes()
    {
        var left = CapsuleLayoutManager.GetMetrics(CapsuleMode.LeftDock, 1920, 1080);
        var right = CapsuleLayoutManager.GetMetrics(CapsuleMode.RightDock, 1920, 1080);

        Assert.True(left.CapsuleHeight > left.CapsuleWidth);
        Assert.True(right.CapsuleHeight > right.CapsuleWidth);
        Assert.Equal(left.CapsuleWidth, right.CapsuleWidth);
    }

    [Fact]
    public void ResolveDropMode_SnapsToTopWhenCloseToTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 400,
            topAfterDrag: 20,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(CapsuleMode.TopIsland, mode);
    }

    [Theory]
    [InlineData(24, CapsuleMode.LeftDock)]
    [InlineData(1860, CapsuleMode.RightDock)]
    public void ResolveDropMode_SnapsToSideDockWhenNearScreenEdge(double leftAfterDrag, CapsuleMode expectedMode)
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: leftAfterDrag,
            topAfterDrag: 360,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(expectedMode, mode);
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
    public void GetWindowFrame_PlacesLeftDockOnLeftEdge()
    {
        var metrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.LeftDock, 1920, 1080);

        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.LeftDock,
            metrics,
            screenWidth: 1920,
            screenHeight: 1080);

        Assert.Equal(0, frame.Left);
        Assert.True(frame.Height > frame.Width);
        Assert.InRange(frame.Top, 120, 280);
    }

    [Fact]
    public void GetWindowFrame_PlacesRightDockOnRightEdge()
    {
        var metrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.RightDock, 1920, 1080);

        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.RightDock,
            metrics,
            screenWidth: 1920,
            screenHeight: 1080);

        Assert.Equal(1920 - frame.Width, frame.Left);
        Assert.True(frame.Height > frame.Width);
        Assert.InRange(frame.Top, 120, 280);
    }
}
