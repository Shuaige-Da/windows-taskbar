using System.Reflection;
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class LyricsServiceTests
{
    [Fact]
    public void GetCurrentLyricSequence_ReturnsCurrentAndUpcomingSyncedLines()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:00.00]第一句
            [00:08.00]第二句
            [00:16.00]第三句
            [00:24.00]第四句
            """);

        var lines = service.GetCurrentLyricSequence(TimeSpan.FromSeconds(9), maxLines: 3);

        Assert.Equal(["第二句", "第三句", "第四句"], lines);
    }

    [Fact]
    public void GetCurrentLyricSequence_ReturnsFirstLinesWhenTimelineIsUnavailable()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:00.00]第一句
            [00:08.00]第二句
            [00:16.00]第三句
            """);

        var lines = service.GetCurrentLyricSequence(TimeSpan.Zero, maxLines: 2);

        Assert.Equal(["第一句", "第二句"], lines);
    }

    [Fact]
    public void GetCurrentLyricDuration_ReturnsTimeUntilNextSyncedLine()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:00.00]第一句
            [00:08.50]第二句
            [00:12.00]第三句
            """);

        var duration = service.GetCurrentLyricDuration(TimeSpan.FromSeconds(9));

        Assert.Equal(TimeSpan.FromSeconds(3.5), duration);
    }

    [Fact]
    public void GetPlaybackWindow_ReturnsOnlyCurrentAndNextTimedLines()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:04.00]第一句
            [00:08.00]第二句
            [00:12.00]第三句
            """);

        var window = service.GetPlaybackWindow(TimeSpan.FromSeconds(9));

        Assert.NotNull(window);
        Assert.Equal("第二句", window.Value.CurrentText);
        Assert.Equal("第三句", window.Value.NextText);
        Assert.Equal(TimeSpan.FromSeconds(8), window.Value.Start);
        Assert.Equal(TimeSpan.FromSeconds(12), window.Value.End);
        Assert.Equal(0.25, window.Value.Progress, 3);
        Assert.Equal(TimeSpan.FromSeconds(3), window.Value.Remaining);
    }

    [Fact]
    public void GetPlaybackWindow_DoesNotShowFirstLyricBeforeItsTimestamp()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:10.00]第一句
            [00:14.00]第二句
            """);

        Assert.Null(service.GetPlaybackWindow(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ParseLrc_AcceptsSecondPrecisionAndMultipleTimestamps()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:03]无小数时间戳
            [00:07.00][00:11.00]重复句
            """);

        Assert.Equal("无小数时间戳", service.GetCurrentLyric(TimeSpan.FromSeconds(3)));
        Assert.Equal("重复句", service.GetCurrentLyric(TimeSpan.FromSeconds(11)));
    }

    [Fact]
    public void ParseLrc_AppliesOffsetToPlaybackWindowTiming()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [offset:+500]
            [00:03.00]延后半秒
            [00:07.00]下一句
            """);

        Assert.Null(service.GetPlaybackWindow(TimeSpan.FromSeconds(3.2)));
        Assert.Equal(
            "延后半秒",
            service.GetPlaybackWindow(TimeSpan.FromSeconds(3.5))?.CurrentText);
    }

    [Fact]
    public void HasLyricsFor_DoesNotExposePreviousSongLyricsUnderNewIdentity()
    {
        var service = new LyricsService();
        LoadLrc(service, "[00:00.00]旧歌曲歌词");
        SetLoadedIdentity(service, "旧歌曲", "旧歌手");

        Assert.True(service.HasLyricsFor("旧歌曲", "旧歌手"));
        Assert.False(service.HasLyricsFor("新歌曲", "新歌手"));
    }

    [Fact]
    public void HasLyricsFor_RejectsSameMetadataWhenDurationIndicatesDifferentVersion()
    {
        var service = new LyricsService();
        LoadLrc(service, "[00:00.00]专辑版歌词");
        SetLoadedIdentity(service, "同名歌曲", "同一歌手");
        typeof(LyricsService).GetField("_lastRequestedDuration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, TimeSpan.FromSeconds(210));
        typeof(LyricsService).GetField("_songDuration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, TimeSpan.FromSeconds(198));

        Assert.True(service.HasLyricsFor("同名歌曲", "同一歌手", TimeSpan.FromSeconds(210)));
        Assert.False(service.HasLyricsFor("同名歌曲", "同一歌手", TimeSpan.FromSeconds(300)));
    }

    [Fact]
    public void HasLyricsFor_AcceptsEquivalentDecoratedMetadata()
    {
        var service = new LyricsService();
        LoadLrc(service, "[00:03.00]歌词");
        SetLoadedIdentity(service, "晴天 (Official Audio)", "周杰伦");

        Assert.True(service.HasLyricsFor("晴天", "周杰伦"));
    }

    [Fact]
    public void GetSongIntroductionWindow_ShowsSongInformationUntilFirstTimedLyric()
    {
        var service = new LyricsService();
        LoadLrc(service, """
            [00:08.00]第一句
            [00:12.00]第二句
            """);
        SetLoadedIdentity(service, "晴天", "周杰伦");

        var introduction = service.GetSongIntroductionWindow(
            "晴天",
            "周杰伦",
            TimeSpan.FromMinutes(4),
            TimeSpan.FromSeconds(2));

        Assert.NotNull(introduction);
        Assert.Equal(-1, introduction.Value.Index);
        Assert.Equal("晴天 · 周杰伦", introduction.Value.CurrentText);
        Assert.Equal("第一句", introduction.Value.NextText);
        Assert.Equal(TimeSpan.FromSeconds(8), introduction.Value.End);
        Assert.Null(service.GetSongIntroductionWindow(
            "晴天",
            "周杰伦",
            TimeSpan.FromMinutes(4),
            TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void GetSongIntroductionWindow_UsesShortFallbackWhileLyricsAreLoading()
    {
        var service = new LyricsService();

        var introduction = service.GetSongIntroductionWindow(
            "新歌曲",
            "新歌手",
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(1));

        Assert.Equal("新歌曲 · 新歌手", introduction?.CurrentText);
        Assert.Equal(TimeSpan.FromSeconds(6), introduction?.End);
        Assert.Null(service.GetSongIntroductionWindow(
            "新歌曲",
            "新歌手",
            TimeSpan.FromMinutes(3),
            TimeSpan.FromSeconds(6)));
    }

    private static void LoadLrc(LyricsService service, string lrc)
    {
        var method = typeof(LyricsService).GetMethod(
            "ParseLrc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, [lrc]);
    }

    private static void SetLoadedIdentity(LyricsService service, string title, string artist)
    {
        typeof(LyricsService).GetField("_lastTitle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, title);
        typeof(LyricsService).GetField("_lastArtist", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, artist);
    }

}
