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
        double edgeThreshold = 72)
    {
        return mode switch
        {
            CapsuleMode.TopIsland => pointerPosition.Y <= edgeThreshold,
            _ => pointerPosition.Y >= Math.Max(screenHeight - edgeThreshold, 0)
        };
    }
}
