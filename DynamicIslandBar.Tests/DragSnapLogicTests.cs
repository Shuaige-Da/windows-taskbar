using DynamicIslandBar;
using System.IO;

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
            topAfterDrag: 820);

        Assert.Equal(CapsuleMode.Floating, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsBottomWhenDroppedNearBottomThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 420,
            topAfterDrag: 1020);

        Assert.Equal(CapsuleMode.BottomTaskbar, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsFloating_WhenLeavingLeftEdgeDuringDrag()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 220,
            topAfterDrag: 400);

        Assert.Equal(CapsuleMode.Floating, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsTopIsland_WhenLeavingLeftEdgeButReenteringTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 220,
            topAfterDrag: 20);

        Assert.Equal(CapsuleMode.TopIsland, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsBottomTaskbar_WhenLeavingRightEdgeButReenteringBottomThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 1680,
            topAfterDrag: 1040);

        Assert.Equal(CapsuleMode.BottomTaskbar, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsRightDock_WhenWithinRightThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 1855,
            topAfterDrag: 320);

        Assert.Equal(CapsuleMode.RightDock, mode);
    }

    [Fact]
    public void GetMetrics_UsesSmallerSystemDensityInTopMode()
    {
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);

        Assert.True(top.CapsuleHeight <= bottom.CapsuleHeight);
    }

    [Fact]
    public void DragEndFallback_UsesReleaseCursorCoordinates_WhenNoPreviewIsActive()
    {
        var source = RepositoryFile.Read("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var releaseCursorPoint = PointToScreen(e.GetPosition(this));", source);
        Assert.Contains("releaseCursorPoint);", source);
    }
}
