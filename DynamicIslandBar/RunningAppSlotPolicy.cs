namespace DynamicIslandBar;

public static class RunningAppSlotPolicy
{
    private const double IconExtent = 44d;
    private const double CenterCardGapIconCount = 2d;
    private const double DefaultSystemAreaExtent = 200d;
    private const double CapsuleChromeReserve = 84d;
    private const int CompactMinSlots = 2;
    private const int BottomMinSlots = 3;
    private const int MaxSlots = 8;

    public static int GetVisibleSlots(
        CapsuleMode mode,
        double capsuleLength,
        double centerCardExtent,
        double systemAreaExtent = DefaultSystemAreaExtent)
    {
        var minSlots = mode is CapsuleMode.TopIsland or CapsuleMode.LeftDock or CapsuleMode.RightDock
            ? CompactMinSlots
            : BottomMinSlots;
        if (capsuleLength <= 0)
        {
            return minSlots;
        }

        var reservedExtent =
            Math.Max(centerCardExtent, 0)
            + Math.Max(systemAreaExtent, 0)
            + (IconExtent * CenterCardGapIconCount)
            + CapsuleChromeReserve;
        var appAreaExtent = Math.Max(0, capsuleLength - reservedExtent);
        var slotsByLength = (int)Math.Floor(appAreaExtent / IconExtent);

        return Math.Clamp(slotsByLength, minSlots, MaxSlots);
    }
}
