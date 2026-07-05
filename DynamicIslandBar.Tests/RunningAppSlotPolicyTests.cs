using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class RunningAppSlotPolicyTests
{
    [Fact]
    public void GetVisibleSlots_KeepsCompactTopIslandSmallAtDefaultLength()
    {
        var slots = RunningAppSlotPolicy.GetVisibleSlots(
            CapsuleMode.TopIsland,
            capsuleLength: 760,
            centerCardExtent: CenterCardLayoutPolicy.MapWidth(CapsuleMode.TopIsland, 760, 58));

        Assert.Equal(3, slots);
    }

    [Theory]
    [InlineData(CapsuleMode.TopIsland)]
    [InlineData(CapsuleMode.LeftDock)]
    [InlineData(CapsuleMode.RightDock)]
    public void GetVisibleSlots_GrowsTopAndSideAppCapacityWithCapsuleLength(CapsuleMode mode)
    {
        var defaultSlots = RunningAppSlotPolicy.GetVisibleSlots(
            mode,
            capsuleLength: 760,
            centerCardExtent: CenterCardLayoutPolicy.MapWidth(mode, 760, 58));
        var expandedSlots = RunningAppSlotPolicy.GetVisibleSlots(
            mode,
            capsuleLength: 1440,
            centerCardExtent: CenterCardLayoutPolicy.MapWidth(mode, 1440, 58));

        Assert.True(expandedSlots > defaultSlots);
        Assert.True(expandedSlots >= 6);
    }

    [Fact]
    public void GetVisibleSlots_ReservesTwoIconGapBesideCenterCard()
    {
        var slots = RunningAppSlotPolicy.GetVisibleSlots(
            CapsuleMode.TopIsland,
            capsuleLength: 1040,
            centerCardExtent: 520,
            systemAreaExtent: 200);

        Assert.Equal(3, slots);
    }
}
