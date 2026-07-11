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

    [Fact]
    public void BuildContinuousTrack_JoinsCurrentAndUpcomingLinesWithReadableGap()
    {
        var track = CenterCardLyricsDanmakuPolicy.BuildContinuousTrack(
            ["第一句", "第二句", "第三句"]);

        Assert.Equal(
            $"第一句{CenterCardLyricsDanmakuPolicy.ContinuousTrackGap}第二句{CenterCardLyricsDanmakuPolicy.ContinuousTrackGap}第三句",
            track);
    }

    [Theory]
    [InlineData(0.4, 4)]
    [InlineData(4, 8)]
    [InlineData(30, 10)]
    public void CalculateSynchronizedTrackDuration_ClampsExtremeSpeeds(
        double lyricSeconds,
        double expectedSeconds)
    {
        var duration = CenterCardLyricsDanmakuPolicy.CalculateSynchronizedTrackDuration(
            TimeSpan.FromSeconds(lyricSeconds),
            totalTravelDistance: 400,
            currentLyricVisibleDistance: 200,
            fallbackDuration: TimeSpan.FromSeconds(6));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), duration);
    }
}
