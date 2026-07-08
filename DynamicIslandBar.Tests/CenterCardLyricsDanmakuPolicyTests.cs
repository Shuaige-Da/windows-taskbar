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

    [Fact]
    public void BuildContinuousTrack_JoinsLyricsWithFourChineseCharacterGap()
    {
        var track = CenterCardLyricsDanmakuPolicy.BuildContinuousTrack([
            "还有雕刻着图案的门帘",
            "还有雕刻着爱的屋檐",
            "风吹过你留下的夏天"
        ]);

        Assert.Equal(
            "还有雕刻着图案的门帘\u3000\u3000\u3000\u3000还有雕刻着爱的屋檐\u3000\u3000\u3000\u3000风吹过你留下的夏天",
            track);
    }

    [Fact]
    public void BuildContinuousTrack_IgnoresBlankLines()
    {
        var track = CenterCardLyricsDanmakuPolicy.BuildContinuousTrack([
            " 第一句 ",
            "",
            "   ",
            "第二句"
        ]);

        Assert.Equal("第一句\u3000\u3000\u3000\u3000第二句", track);
    }

    [Fact]
    public void FormatVerticalTrack_PreservesFourCharacterGapBetweenLyricLines()
    {
        var track = CenterCardLyricsDanmakuPolicy.BuildContinuousTrack([
            "第一句",
            "第二句"
        ]);

        var verticalTrack = CenterCardLyricsDanmakuPolicy.FormatVerticalTrack(track);

        Assert.Equal(
            "第\r\n一\r\n句\r\n\r\n\r\n\r\n\r\n第\r\n二\r\n句".Replace("\r\n", Environment.NewLine),
            verticalTrack);
    }

    [Fact]
    public void CalculateSynchronizedTrackDuration_MapsCurrentLyricVisibleTimeToAudioDuration()
    {
        var duration = CenterCardLyricsDanmakuPolicy.CalculateSynchronizedTrackDuration(
            currentLyricDuration: TimeSpan.FromSeconds(4),
            totalTravelDistance: 600,
            currentLyricVisibleDistance: 200,
            fallbackDuration: TimeSpan.FromSeconds(8));

        Assert.Equal(TimeSpan.FromSeconds(12), duration);
    }

    [Fact]
    public void CalculateSynchronizedTrackDuration_UsesFallbackWhenLyricTimingIsMissing()
    {
        var duration = CenterCardLyricsDanmakuPolicy.CalculateSynchronizedTrackDuration(
            currentLyricDuration: TimeSpan.Zero,
            totalTravelDistance: 600,
            currentLyricVisibleDistance: 200,
            fallbackDuration: TimeSpan.FromSeconds(8));

        Assert.Equal(TimeSpan.FromSeconds(8), duration);
    }

    [Fact]
    public void ShouldRestartMarquee_KeepsActiveHorizontalTrackUntilItFinishes()
    {
        var shouldRestart = CenterCardLyricsDanmakuPolicy.ShouldRestartMarquee(
            isActive: true,
            usesVerticalLyricsFlow: false,
            activeTrackCount: 1,
            activeText: "第一句\u3000\u3000\u3000\u3000第二句",
            nextText: "第二句\u3000\u3000\u3000\u3000第三句");

        Assert.False(shouldRestart);
    }

    [Fact]
    public void ShouldRestartMarquee_KeepsActiveSideTrackUntilItFinishes()
    {
        var shouldRestart = CenterCardLyricsDanmakuPolicy.ShouldRestartMarquee(
            isActive: true,
            usesVerticalLyricsFlow: true,
            activeTrackCount: 1,
            activeText: "第一句\u3000\u3000\u3000\u3000第二句",
            nextText: "第二句\u3000\u3000\u3000\u3000第三句");

        Assert.False(shouldRestart);
    }

    [Fact]
    public void ShouldRestartMarquee_RestartsWhenHorizontalTrackHasFinished()
    {
        var shouldRestart = CenterCardLyricsDanmakuPolicy.ShouldRestartMarquee(
            isActive: true,
            usesVerticalLyricsFlow: false,
            activeTrackCount: 0,
            activeText: "第一句\u3000\u3000\u3000\u3000第二句",
            nextText: "第二句\u3000\u3000\u3000\u3000第三句");

        Assert.True(shouldRestart);
    }
}
