using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CenterCardLyricsDanmakuPolicyTests
{
    [Fact]
    public void RegisterLyric_EnqueuesFirstLyricIntoFirstLane()
    {
        var state = CenterCardLyricsDanmakuPolicy.RegisterLyric(
            previousLyric: null,
            newLyric: "第一句",
            nextLaneIndex: 0,
            laneCount: 3);

        Assert.True(state.ShouldEnqueue);
        Assert.Equal(0, state.LaneIndex);
        Assert.Equal(1, state.NextLaneIndex);
    }

    [Fact]
    public void RegisterLyric_DoesNotEnqueueRepeatedLyric()
    {
        var state = CenterCardLyricsDanmakuPolicy.RegisterLyric(
            previousLyric: "第一句",
            newLyric: "第一句",
            nextLaneIndex: 1,
            laneCount: 3);

        Assert.False(state.ShouldEnqueue);
        Assert.Equal(-1, state.LaneIndex);
        Assert.Equal(1, state.NextLaneIndex);
    }

    [Fact]
    public void RegisterLyric_WrapsLaneIndex()
    {
        var first = CenterCardLyricsDanmakuPolicy.RegisterLyric(null, "第一句", 2, 3);
        var second = CenterCardLyricsDanmakuPolicy.RegisterLyric("第一句", "第二句", first.NextLaneIndex, 3);

        Assert.True(first.ShouldEnqueue);
        Assert.Equal(2, first.LaneIndex);
        Assert.Equal(0, first.NextLaneIndex);
        Assert.True(second.ShouldEnqueue);
        Assert.Equal(0, second.LaneIndex);
    }
}
