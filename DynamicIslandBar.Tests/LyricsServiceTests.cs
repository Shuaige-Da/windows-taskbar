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
