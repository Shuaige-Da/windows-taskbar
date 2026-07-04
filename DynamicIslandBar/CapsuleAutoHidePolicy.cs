using System.Windows;

namespace DynamicIslandBar;

public static class CapsuleAutoHidePolicy
{
    public static bool CanHide(bool isDragging, bool isPointerOverCapsule, bool hasOpenPopup)
    {
        return !isDragging && !isPointerOverCapsule && !hasOpenPopup;
    }

    public static bool IsPointerInRevealZone(
        CapsuleMode mode,
        Point pointerPosition,
        double screenWidth,
        double screenHeight,
        Rect? floatingRevealBounds = null,
        double floatingRevealPadding = 36,
        double edgeThreshold = 72)
    {
        return mode switch
        {
            CapsuleMode.TopIsland => pointerPosition.Y <= edgeThreshold,
            CapsuleMode.Floating when floatingRevealBounds is Rect bounds => IsPointerWithinFloatingRevealBounds(
                pointerPosition,
                bounds,
                floatingRevealPadding),
            CapsuleMode.Floating => false,
            _ => pointerPosition.Y >= Math.Max(screenHeight - edgeThreshold, 0)
        };
    }

    private static bool IsPointerWithinFloatingRevealBounds(
        Point pointerPosition,
        Rect floatingRevealBounds,
        double floatingRevealPadding)
    {
        var revealBounds = floatingRevealBounds;
        revealBounds.Inflate(floatingRevealPadding, floatingRevealPadding);
        return revealBounds.Contains(pointerPosition);
    }
}
