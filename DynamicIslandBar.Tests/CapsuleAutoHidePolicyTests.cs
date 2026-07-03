using System.Windows;
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleAutoHidePolicyTests
{
    [Fact]
    public void CanHide_ReturnsTrue_WhenCapsuleIsIdle()
    {
        var canHide = CapsuleAutoHidePolicy.CanHide(
            isDragging: false,
            isPointerOverCapsule: false,
            hasOpenPopup: false);

        Assert.True(canHide);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CanHide_ReturnsFalse_WhenInteractionIsStillActive(
        bool isDragging,
        bool isPointerOverCapsule,
        bool hasOpenPopup)
    {
        var canHide = CapsuleAutoHidePolicy.CanHide(
            isDragging,
            isPointerOverCapsule,
            hasOpenPopup);

        Assert.False(canHide);
    }

    [Fact]
    public void IsPointerInRevealZone_ReturnsTrue_ForBottomTaskbarNearBottomEdge()
    {
        var shouldReveal = CapsuleAutoHidePolicy.IsPointerInRevealZone(
            CapsuleMode.BottomTaskbar,
            new Point(960, 1045),
            screenWidth: 1920,
            screenHeight: 1080);

        Assert.True(shouldReveal);
    }

    [Fact]
    public void IsPointerInRevealZone_ReturnsTrue_ForTopIslandNearTopEdge()
    {
        var shouldReveal = CapsuleAutoHidePolicy.IsPointerInRevealZone(
            CapsuleMode.TopIsland,
            new Point(960, 24),
            screenWidth: 1920,
            screenHeight: 1080);

        Assert.True(shouldReveal);
    }

    [Fact]
    public void IsPointerInRevealZone_ReturnsFalse_WhenPointerIsAwayFromRelevantEdge()
    {
        var shouldReveal = CapsuleAutoHidePolicy.IsPointerInRevealZone(
            CapsuleMode.BottomTaskbar,
            new Point(960, 540),
            screenWidth: 1920,
            screenHeight: 1080);

        Assert.False(shouldReveal);
    }
}
