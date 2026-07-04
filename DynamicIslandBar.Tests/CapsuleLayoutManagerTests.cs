using System.Windows;
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
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

        Assert.Equal(top.CapsuleWidth, left.CapsuleWidth);
        Assert.Equal(top.CapsuleHeight, left.CapsuleHeight);
        Assert.Equal(top.VisibleAppSlots, left.VisibleAppSlots);
        Assert.Equal(top.CapsuleWidth, right.CapsuleWidth);
        Assert.Equal(top.CapsuleHeight, right.CapsuleHeight);
        Assert.Equal(top.VisibleAppSlots, right.VisibleAppSlots);
        Assert.Equal(left.CapsuleWidth, right.CapsuleWidth);
        Assert.Equal(PopupFlowDirection.Right, left.PopupDirection);
        Assert.Equal(PopupFlowDirection.Left, right.PopupDirection);
    }

    [Fact]
    public void GetMetrics_UsesTopPopupDefaultsForSideDockModes()
    {
        var left = CapsuleLayoutManager.GetMetrics(CapsuleMode.LeftDock, 1920, 1080);
        var right = CapsuleLayoutManager.GetMetrics(CapsuleMode.RightDock, 1920, 1080);
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

        Assert.Equal(top.CapsuleWidth, left.CapsuleWidth);
        Assert.Equal(top.CapsuleHeight, left.CapsuleHeight);
        Assert.Equal(PopupFlowDirection.Right, left.PopupDirection);
        Assert.Equal(PopupFlowDirection.Left, right.PopupDirection);
    }

    [Fact]
    public void GetMetrics_UsesBottomMetricsForFloatingMode()
    {
        var floating = CapsuleLayoutManager.GetMetrics(CapsuleMode.Floating, 1920, 1080);
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);

        Assert.Equal(bottom.CapsuleWidth, floating.CapsuleWidth);
        Assert.Equal(bottom.CapsuleHeight, floating.CapsuleHeight);
        Assert.Equal(bottom.VisibleAppSlots, floating.VisibleAppSlots);
        Assert.Equal(bottom.PopupDirection, floating.PopupDirection);
    }

    [Fact]
    public void ResolveDropMode_SnapsToTopWhenCloseToTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 400,
            topAfterDrag: 20);

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
            topAfterDrag: 360);

        Assert.Equal(expectedMode, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsFloating_WhenDroppedAwayFromAllEdges()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 640,
            topAfterDrag: 420);

        Assert.Equal(CapsuleMode.Floating, mode);
    }

    [Fact]
    public void ResolveDropMode_ReturnsBottomTaskbar_WhenDroppedNearBottomEdge()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenWidth: 1920,
            screenHeight: 1080,
            leftAfterDrag: 640,
            topAfterDrag: 1040);

        Assert.Equal(CapsuleMode.BottomTaskbar, mode);
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
        Assert.Equal(metrics.CapsuleHeight + 24, frame.Width, precision: 1);
        Assert.Equal(metrics.CapsuleWidth + 40, frame.Height, precision: 1);
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
        Assert.Equal(metrics.CapsuleHeight + 24, frame.Width, precision: 1);
        Assert.Equal(metrics.CapsuleWidth + 40, frame.Height, precision: 1);
    }

    [Fact]
    public void GetWindowFrame_UsesFloatingCoordinates_ForFloatingMode()
    {
        var metrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.Floating, 1920, 1080);

        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.Floating,
            metrics,
            screenWidth: 1920,
            screenHeight: 1080,
            floatingLeft: 360,
            floatingTop: 240);

        Assert.Equal(360, frame.Left, precision: 1);
        Assert.Equal(240, frame.Top, precision: 1);
    }

    [Fact]
    public void BuildSnapPreview_UsesRotatedTopStyleContract_ForLeftDock()
    {
        var preview = CapsuleLayoutManager.BuildSnapPreview(
            SnapEdge.Left,
            screenWidth: 1920,
            screenHeight: 1080,
            topCapsuleWidth: 760,
            topCapsuleHeight: 72,
            bottomCapsuleWidth: 1320,
            bottomCapsuleHeight: 80);

        Assert.Equal(CapsuleMode.LeftDock, preview.Mode);
        Assert.Equal(90, preview.RotationDegrees, precision: 1);
        Assert.Equal(72 + 24, preview.Frame.Width, precision: 1);
        Assert.Equal(760 + 40, preview.Frame.Height, precision: 1);
    }

    [Fact]
    public void ResolveBottomPreviewCapsuleSize_UsesLastSavedBottomMetrics_WhenAvailable()
    {
        var size = CapsuleLayoutManager.ResolveBottomPreviewCapsuleSize(
            fallbackWidth: 1440,
            fallbackHeight: 80,
            lastBottomCapsuleWidth: 1180,
            lastBottomCapsuleHeight: 64);

        Assert.Equal(1180, size.Width);
        Assert.Equal(64, size.Height);
    }

    [Theory]
    [InlineData(0, 64)]
    [InlineData(1180, 0)]
    [InlineData(-1, 64)]
    [InlineData(1180, -1)]
    public void ResolveBottomPreviewCapsuleSize_FallsBack_WhenSavedMetricsAreMissing(
        double lastBottomCapsuleWidth,
        double lastBottomCapsuleHeight)
    {
        var size = CapsuleLayoutManager.ResolveBottomPreviewCapsuleSize(
            fallbackWidth: 1440,
            fallbackHeight: 80,
            lastBottomCapsuleWidth,
            lastBottomCapsuleHeight);

        Assert.Equal(1440, size.Width);
        Assert.Equal(80, size.Height);
    }

    [Fact]
    public void GetCapsuleBounds_UsesTopAlignedOrigin_ForTopIsland()
    {
        var topMetrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);
        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.TopIsland,
            topMetrics,
            screenWidth: 1920,
            screenHeight: 1080);

        var visibleBounds = CapsuleLayoutManager.GetCapsuleBounds(
            CapsuleMode.TopIsland,
            frame,
            renderedCapsuleWidth: 760,
            renderedCapsuleHeight: 72);

        Assert.Equal(frame.Left + 20, visibleBounds.Left, precision: 1);
        Assert.Equal(0, visibleBounds.Top, precision: 1);
        Assert.Equal(760, visibleBounds.Width, precision: 1);
        Assert.Equal(72, visibleBounds.Height, precision: 1);
    }

    [Fact]
    public void GetCapsuleBounds_UsesUprightSideDockBounds_ForRealCapsule()
    {
        var sideMetrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.LeftDock, 1920, 1080);
        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.LeftDock,
            sideMetrics,
            screenWidth: 1920,
            screenHeight: 1080);

        var visibleBounds = CapsuleLayoutManager.GetCapsuleBounds(
            CapsuleMode.LeftDock,
            frame,
            renderedCapsuleWidth: 72,
            renderedCapsuleHeight: 760);

        Assert.Equal(frame.Left + 12, visibleBounds.Left, precision: 1);
        Assert.Equal(frame.Top + 20, visibleBounds.Top, precision: 1);
        Assert.Equal(72, visibleBounds.Width, precision: 1);
        Assert.Equal(760, visibleBounds.Height, precision: 1);
    }

    [Fact]
    public void GetFloatingWindowOriginForVisibleCapsule_PreservesTopIslandVisiblePosition_WithMappedRenderedHeight()
    {
        var topMetrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);
        var topRenderedHeight = CapsuleAppearanceMapper.MapCapsuleHeight(topMetrics.CapsuleHeight, 40);
        var topFrame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.TopIsland,
            topMetrics,
            screenWidth: 1920,
            screenHeight: 1080);
        var sourceVisibleBounds = CapsuleLayoutManager.GetCapsuleBounds(
            CapsuleMode.TopIsland,
            topFrame,
            renderedCapsuleWidth: 760,
            renderedCapsuleHeight: topRenderedHeight);

        var floatingMetrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.Floating, 1920, 1080);
        var floatingRenderedHeight = CapsuleAppearanceMapper.MapCapsuleHeight(floatingMetrics.CapsuleHeight, 40);

        var origin = CapsuleLayoutManager.GetFloatingWindowOriginForVisibleCapsule(
            renderedFloatingCapsuleWidth: floatingMetrics.CapsuleWidth,
            renderedFloatingCapsuleHeight: floatingRenderedHeight,
            sourceVisibleBounds.Left,
            sourceVisibleBounds.Top);

        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.Floating,
            floatingMetrics,
            screenWidth: 1920,
            screenHeight: 1080,
            floatingLeft: origin.X,
            floatingTop: origin.Y);
        var visibleBounds = CapsuleLayoutManager.GetCapsuleBounds(
            CapsuleMode.Floating,
            frame,
            renderedCapsuleWidth: floatingMetrics.CapsuleWidth,
            renderedCapsuleHeight: floatingRenderedHeight);

        Assert.Equal(sourceVisibleBounds.Left, visibleBounds.Left, precision: 1);
        Assert.Equal(sourceVisibleBounds.Top, visibleBounds.Top, precision: 1);
        Assert.Equal(topRenderedHeight, sourceVisibleBounds.Height, precision: 1);
        Assert.NotEqual(floatingMetrics.CapsuleHeight, floatingRenderedHeight, precision: 1);
    }

    [Fact]
    public void ClampWindowOriginToVisibleBounds_KeepsSideDockCapsuleFullyRecoverable()
    {
        var clamped = CapsuleLayoutManager.ClampWindowOriginToVisibleBounds(
            CapsuleMode.LeftDock,
            desiredLeft: -120,
            desiredTop: -80,
            frameWidth: 96,
            frameHeight: 800,
            screenWidth: 1920,
            screenHeight: 1080,
            renderedCapsuleWidth: 72,
            renderedCapsuleHeight: 760);

        Assert.Equal(-12, clamped.X, precision: 1);
        Assert.Equal(-20, clamped.Y, precision: 1);
    }

    [Fact]
    public void ClampWindowOriginToVisibleBounds_PullsFloatingCapsuleBackInsideScreen()
    {
        var clamped = CapsuleLayoutManager.ClampWindowOriginToVisibleBounds(
            CapsuleMode.Floating,
            desiredLeft: 400,
            desiredTop: 900,
            frameWidth: 1960,
            frameHeight: 420,
            screenWidth: 1920,
            screenHeight: 1080,
            renderedCapsuleWidth: 1920,
            renderedCapsuleHeight: 80);

        Assert.Equal(-20, clamped.X, precision: 1);
        Assert.Equal(830, clamped.Y, precision: 1);
    }
}
