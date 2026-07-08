using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleSnapPreviewTests
{
    [Theory]
    [InlineData(SnapEdge.Top, CapsuleMode.TopIsland, 0)]
    [InlineData(SnapEdge.Left, CapsuleMode.LeftDock, 90)]
    [InlineData(SnapEdge.Right, CapsuleMode.RightDock, 90)]
    public void BuildSnapPreview_UsesTopCapsuleMetrics_ForTopLeftAndRightEdges(
        SnapEdge edge,
        CapsuleMode expectedMode,
        double expectedRotationDegrees)
    {
        var preview = CapsuleLayoutManager.BuildSnapPreview(
            edge,
            screenWidth: 1920,
            screenHeight: 1080,
            topCapsuleWidth: 760,
            topCapsuleHeight: 72,
            bottomCapsuleWidth: 1500,
            bottomCapsuleHeight: 80);

        Assert.Equal(edge, preview.Edge);
        Assert.Equal(expectedMode, preview.Mode);
        Assert.Equal(760, preview.CapsuleWidth, precision: 1);
        Assert.Equal(72, preview.CapsuleHeight, precision: 1);
        Assert.Equal(expectedRotationDegrees, preview.RotationDegrees, precision: 1);
    }

    [Theory]
    [InlineData(SnapEdge.Left, 0)]
    [InlineData(SnapEdge.Right, 1920 - 84)]
    public void BuildSnapPreview_UsesVerticalFrame_ForSideDockPreviews(
        SnapEdge edge,
        double expectedLeft)
    {
        var preview = CapsuleLayoutManager.BuildSnapPreview(
            edge,
            screenWidth: 1920,
            screenHeight: 1080,
            topCapsuleWidth: 760,
            topCapsuleHeight: 72,
            bottomCapsuleWidth: 1500,
            bottomCapsuleHeight: 80);

        Assert.Equal(expectedLeft, preview.Frame.Left, precision: 1);
        Assert.Equal(140, preview.Frame.Top, precision: 1);
        Assert.Equal(84, preview.Frame.Width, precision: 1);
        Assert.Equal(800, preview.Frame.Height, precision: 1);
        Assert.True(preview.Frame.Height > preview.Frame.Width);
    }

    [Fact]
    public void BuildSnapPreview_UsesLastBottomMetrics_ForBottomEdge()
    {
        var preview = CapsuleLayoutManager.BuildSnapPreview(
            SnapEdge.Bottom,
            screenWidth: 1920,
            screenHeight: 1080,
            topCapsuleWidth: 760,
            topCapsuleHeight: 72,
            bottomCapsuleWidth: 1320,
            bottomCapsuleHeight: 80);

        Assert.Equal(SnapEdge.Bottom, preview.Edge);
        Assert.Equal(CapsuleMode.BottomTaskbar, preview.Mode);
        Assert.Equal(1320, preview.CapsuleWidth, precision: 1);
        Assert.Equal(80, preview.CapsuleHeight, precision: 1);
        Assert.Equal(0, preview.RotationDegrees, precision: 1);
        Assert.Equal(1360, preview.Frame.Width, precision: 1);
        Assert.Equal(420, preview.Frame.Height, precision: 1);
    }
}
