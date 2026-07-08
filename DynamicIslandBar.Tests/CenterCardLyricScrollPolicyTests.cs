using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CenterCardLyricScrollPolicyTests
{
    [Fact]
    public void BuildPlan_ShortLineStillScrollsWithReadableMinimumDuration()
    {
        var plan = CenterCardLyricScrollPolicy.BuildHorizontalPlan(
            viewportWidth: 240,
            textWidth: 56,
            lineLifetime: TimeSpan.FromSeconds(0.9));

        Assert.True(plan.Distance > 0);
        Assert.True(plan.Duration >= TimeSpan.FromSeconds(1.8));
    }

    [Fact]
    public void BuildPlan_ShortLineStartsVisibleAtRightEdgeAndScrollsLeftLikeDanmaku()
    {
        var plan = CenterCardLyricScrollPolicy.BuildHorizontalPlan(
            viewportWidth: 240,
            textWidth: 56,
            lineLifetime: TimeSpan.FromSeconds(4));

        Assert.Equal(184, plan.StartOffset);
        Assert.True(plan.EndOffset < 0);
        Assert.True(plan.Distance >= 184 + 56);
    }

    [Fact]
    public void BuildPlan_LongLineUsesOverflowDistance()
    {
        var plan = CenterCardLyricScrollPolicy.BuildHorizontalPlan(
            viewportWidth: 240,
            textWidth: 420,
            lineLifetime: TimeSpan.FromSeconds(4));

        Assert.Equal(0, plan.StartOffset);
        Assert.True(plan.EndOffset < 0);
        Assert.True(plan.Distance >= 180);
        Assert.Equal(TimeSpan.FromSeconds(4), plan.Duration);
    }

    [Fact]
    public void BuildVerticalPlan_UsesViewportHeightForSideDockLyrics()
    {
        var plan = CenterCardLyricScrollPolicy.BuildVerticalPlan(
            viewportHeight: 180,
            textHeight: 96,
            lineLifetime: TimeSpan.FromSeconds(3));

        Assert.True(plan.StartOffset > 0);
        Assert.True(plan.EndOffset < 0);
    }
}
