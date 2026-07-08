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
    public void RegisterLyric_DoesNotEnqueueRepeatedLyric()
    {
        var state = CenterCardLyricsDanmakuPolicy.RegisterLyric(
            previousLyric: "第一句",
            newLyric: "第一句",
            nextLaneIndex: 0,
            laneCount: 1);

        Assert.False(state.ShouldEnqueue);
    }
}
