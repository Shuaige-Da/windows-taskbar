using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class DragSnapLogicTests
{
    [Fact]
    public void ResolveDropMode_ReturnsBottomWhenDroppedAwayFromTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920, screenHeight: 1080,
            leftAfterDrag: 900, topAfterDrag: 820,
            currentMode: CapsuleMode.TopIsland);

        Assert.Equal(CapsuleMode.BottomTaskbar, mode);
    }

    [Fact]
    public void ResolveDropMode_SnapsToLeftDockWhenDroppedNearLeftEdge()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920, screenHeight: 1080,
            leftAfterDrag: 0, topAfterDrag: 540,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(CapsuleMode.LeftDock, mode);
    }

    [Fact]
    public void ResolveDropMode_SnapsToRightDockWhenDroppedNearRightEdge()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920, screenHeight: 1080,
            leftAfterDrag: 1800, topAfterDrag: 540,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(CapsuleMode.RightDock, mode);
    }

    [Fact]
    public void ResolveDropMode_PreservesSideDockWhenDroppedBetweenEdges()
    {
        var left = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920, screenHeight: 1080,
            leftAfterDrag: 900, topAfterDrag: 540,
            currentMode: CapsuleMode.LeftDock);

        var right = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920, screenHeight: 1080,
            leftAfterDrag: 900, topAfterDrag: 540,
            currentMode: CapsuleMode.RightDock);

        Assert.Equal(CapsuleMode.LeftDock, left);
        Assert.Equal(CapsuleMode.RightDock, right);
    }

    [Fact]
    public void GetMetrics_UsesSmallerSystemDensityInTopMode()
    {
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);

        Assert.True(top.CapsuleHeight <= bottom.CapsuleHeight);
    }
}
