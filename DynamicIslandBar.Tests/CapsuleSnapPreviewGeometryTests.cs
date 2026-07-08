using System.Windows;

namespace DynamicIslandBar.Tests;

public class CapsuleSnapPreviewGeometryTests
{
    [Fact]
    public void ComputeOutlineOrigin_CentersUnrotatedPreviewWithinFrame()
    {
        var origin = CapsuleSnapPreviewGeometry.ComputeOutlineOrigin(
            new WindowFrame(100, 200, 800, 420),
            CapsuleMode.Floating,
            capsuleWidth: 760,
            capsuleHeight: 72,
            rotationDegrees: 0);

        Assert.Equal(120, origin.X, 3);
        Assert.Equal(374, origin.Y, 3);
    }

    [Fact]
    public void ComputeOutlineOrigin_TopIslandPreviewAlignsWithScreenTop()
    {
        var origin = CapsuleSnapPreviewGeometry.ComputeOutlineOrigin(
            new WindowFrame(560, 0, 800, 420),
            CapsuleMode.TopIsland,
            capsuleWidth: 760,
            capsuleHeight: 72,
            rotationDegrees: 0);

        Assert.Equal(580, origin.X, 3);
        Assert.Equal(0, origin.Y, 3);
    }

    [Fact]
    public void ComputeOutlineOrigin_AlignsRotatedPreviewBoundsWithinSideFrame()
    {
        var origin = CapsuleSnapPreviewGeometry.ComputeOutlineOrigin(
            new WindowFrame(0, 220, 96, 800),
            CapsuleMode.LeftDock,
            capsuleWidth: 760,
            capsuleHeight: 72,
            rotationDegrees: 90);

        Assert.Equal(-332, origin.X, 3);
        Assert.Equal(584, origin.Y, 3);

        var bounds = CapsuleSnapPreviewGeometry.ComputeRenderedBounds(
            origin,
            capsuleWidth: 760,
            capsuleHeight: 72,
            rotationDegrees: 90);

        Assert.Equal(12, bounds.Left, 3);
        Assert.Equal(240, bounds.Top, 3);
        Assert.Equal(72, bounds.Width, 3);
        Assert.Equal(760, bounds.Height, 3);
    }
}
