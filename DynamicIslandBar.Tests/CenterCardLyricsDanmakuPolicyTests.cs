using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CenterCardLyricsDanmakuPolicyTests
{
    [Fact]
    public void FormatVerticalTrack_ReturnsEmptyStringForBlankInput()
    {
        Assert.Equal(string.Empty, CenterCardLyricsDanmakuPolicy.FormatVerticalTrack(string.Empty));
    }

    [Fact]
    public void BuildLineMotionPlan_KeepsCurrentLineMovementGentleForShortText()
    {
        var plan = CenterCardLyricsDanmakuPolicy.BuildLineMotionPlan(
            viewportExtent: 500,
            currentTextExtent: 180,
            nextTextExtent: 160,
            lineDuration: TimeSpan.FromSeconds(8),
            progress: 0.25);

        Assert.Equal(310, plan.CurrentStartOffset, 2);
        Assert.Equal(230, plan.CurrentEndOffset, 2);
        Assert.Equal(TimeSpan.FromSeconds(6), plan.RemainingDuration);
    }

    [Fact]
    public void BuildLineMotionPlan_RevealsNextLineOnlyNearCurrentLineEnd()
    {
        var plan = CenterCardLyricsDanmakuPolicy.BuildLineMotionPlan(
            viewportExtent: 500,
            currentTextExtent: 180,
            nextTextExtent: 160,
            lineDuration: TimeSpan.FromSeconds(8),
            progress: 0.8);

        Assert.InRange(plan.NextRevealDelay.TotalSeconds, 0.09, 0.11);
        Assert.Equal(TimeSpan.FromSeconds(1.5), plan.NextRevealDuration);
        Assert.True(plan.NextStartOffset > plan.NextEndOffset);
    }

    [Fact]
    public void BuildLineMotionPlan_ScrollsLongCurrentLineFarEnoughToReadItsEnd()
    {
        var plan = CenterCardLyricsDanmakuPolicy.BuildLineMotionPlan(
            viewportExtent: 320,
            currentTextExtent: 520,
            nextTextExtent: 180,
            lineDuration: TimeSpan.FromSeconds(6),
            progress: 0);

        Assert.Equal(-212, plan.CurrentEndOffset, 2);
    }
}
