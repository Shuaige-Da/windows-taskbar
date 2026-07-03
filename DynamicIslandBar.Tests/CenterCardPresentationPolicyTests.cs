using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CenterCardPresentationPolicyTests
{
    [Fact]
    public void Build_UsesLyricsMarqueeForPlayingMusicWhenNotHovered()
    {
        var app = CreateApp("cloudmusic", "网易云音乐");
        var media = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "像鱼",
            Artist: "王贰浪",
            Lyric: "我在黄昏里等风经过");

        var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: false);

        Assert.Equal(CenterCardDisplayMode.MusicLyricsMarquee, state.Mode);
        Assert.Equal("我在黄昏里等风经过", state.PrimaryText);
        Assert.True(state.ShowLyricsMarquee);
        Assert.False(state.ShowTransportControls);
        Assert.False(state.ShowAppActions);
    }

    [Fact]
    public void Build_UsesMusicDetailsForPlayingMusicWhenHovered()
    {
        var app = CreateApp("cloudmusic", "网易云音乐");
        var media = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "像鱼",
            Artist: "王贰浪",
            Lyric: "我在黄昏里等风经过");

        var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: true);

        Assert.Equal(CenterCardDisplayMode.MusicDetails, state.Mode);
        Assert.Equal("像鱼 - 王贰浪", state.PrimaryText);
        Assert.Equal("歌词：我在黄昏里等风经过", state.SecondaryText);
        Assert.True(state.ShowTransportControls);
        Assert.False(state.ShowLyricsMarquee);
    }

    [Fact]
    public void Build_UsesMusicDetailsForPausedMusicWhenHovered()
    {
        var app = CreateApp("cloudmusic", "网易云音乐");
        var media = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: false,
            Title: "Take Me Hand",
            Artist: "DAISHI DANCE",
            Lyric: "Take Me Hand - DAISHI DANCE");

        var state = CenterCardPresentationPolicy.Build(app, "中心应用", media, isHovered: true);

        Assert.Equal(CenterCardDisplayMode.MusicDetails, state.Mode);
        Assert.Equal("Take Me Hand - DAISHI DANCE", state.PrimaryText);
        Assert.Equal("已暂停", state.SecondaryText);
        Assert.True(state.ShowTransportControls);
        Assert.False(state.ShowLyricsMarquee);
    }

    [Fact]
    public void MediaSnapshot_CarriesTimelineForProgressAndLyricSync()
    {
        Assert.NotNull(typeof(CenterCardMediaSnapshot).GetProperty("Position"));
        Assert.NotNull(typeof(CenterCardMediaSnapshot).GetProperty("Duration"));
    }

    [Fact]
    public void Build_ReportsProgressForMusicDetailsWhenTimelineExists()
    {
        var app = CreateApp("cloudmusic", "网易云音乐");
        var media = CreateMediaSnapshotWithTimeline(
            title: "像鱼",
            artist: "王贰浪",
            lyric: "我在黄昏里等风经过",
            position: TimeSpan.FromSeconds(75),
            duration: TimeSpan.FromSeconds(300));

        var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: true);

        var progressRatio = Assert.IsType<double>(GetRequiredProperty(state, "ProgressRatio"));
        Assert.Equal(0.25d, progressRatio, precision: 3);
        Assert.Equal("01:15 / 05:00", GetRequiredProperty(state, "ProgressText"));
    }

    [Fact]
    public void Build_AlwaysUsesAppDetailsForNonMusicApps()
    {
        var app = CreateApp("codex", "Codex");

        var normal = CenterCardPresentationPolicy.Build(app, "当前窗口", null, isHovered: false);
        var hovered = CenterCardPresentationPolicy.Build(app, "当前窗口", null, isHovered: true);

        Assert.Equal(CenterCardDisplayMode.AppDetails, normal.Mode);
        Assert.Equal(CenterCardDisplayMode.AppDetails, hovered.Mode);
        Assert.Equal("Codex", normal.PrimaryText);
        Assert.Equal("当前窗口 · 单击激活 / 再次单击最小化", normal.SecondaryText);
        Assert.True(normal.ShowAppActions);
        Assert.True(hovered.ShowAppActions);
    }

    [Fact]
    public void Build_ReturnsEmptyWhenNoAppExists()
    {
        var state = CenterCardPresentationPolicy.Build(null, "当前窗口", null, isHovered: false);

        Assert.Equal(CenterCardDisplayMode.Hidden, state.Mode);
        Assert.Equal(string.Empty, state.PrimaryText);
    }

    [Fact]
    public void Resolve_UsesMatchingLiveSnapshotBeforeFallback()
    {
        var app = CreateApp("d:\\qqmusic\\qqmusic.exe", "QQ音乐");
        var live = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "实时标题",
            Artist: "实时歌手",
            Lyric: "实时歌词",
            SourceAppUserModelId: "QQMusic");

        var snapshot = CenterCardMediaSnapshotProvider.Resolve(app, live);

        Assert.NotNull(snapshot);
        Assert.Equal("实时标题", snapshot!.Title);
        Assert.Equal("实时歌手", snapshot.Artist);
        Assert.Equal("实时歌词", snapshot.Lyric);
    }

    [Fact]
    public void PreserveResolvedFields_KeepsLyricAndEstimatedTimelineForSameSong()
    {
        var previous = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "Beautiful Girls",
            Artist: "A-Mac",
            Lyric: "All the beautiful girls",
            SourceAppUserModelId: "cloudmusic.exe",
            Position: TimeSpan.FromSeconds(35),
            Duration: TimeSpan.FromSeconds(180));
        var fresh = previous with
        {
            Lyric = string.Empty,
            Position = null,
            Duration = null
        };

        var merged = CenterCardMediaSnapshotProvider.PreserveResolvedFields(fresh, previous);

        Assert.Equal("All the beautiful girls", merged.Lyric);
        Assert.Equal(TimeSpan.FromSeconds(35), merged.Position);
        Assert.Equal(TimeSpan.FromSeconds(180), merged.Duration);
    }

    [Fact]
    public void Resolve_IgnoresLiveSnapshotFromAnotherAppWithoutFakeFallback()
    {
        var app = CreateApp("d:\\qqmusic\\qqmusic.exe", "QQ音乐");
        var live = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "浏览器声音",
            Artist: "网页",
            Lyric: "不应该串台",
            SourceAppUserModelId: "Chrome");

        var snapshot = CenterCardMediaSnapshotProvider.Resolve(app, live);

        Assert.Null(snapshot);
    }

    [Fact]
    public void SourceLooksLikeApp_MatchesExeAndDisplayName()
    {
        var app = CreateApp("d:\\qqmusic\\qqmusic.exe", "QQ音乐");

        Assert.True(CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, "QQMusic"));
        Assert.True(CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, "QQ音乐"));
        Assert.False(CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, "Chrome"));
    }

    [Theory]
    [InlineData(@"c:\apps\vlc\vlc.exe", "VLC media player")]
    [InlineData(@"c:\apps\potplayer\potplayermini64.exe", "PotPlayer")]
    [InlineData(@"c:\apps\foobar2000\foobar2000.exe", "foobar2000")]
    [InlineData(@"c:\apps\aimp\aimp.exe", "AIMP")]
    [InlineData(@"c:\program files\windows media player\wmplayer.exe", "Windows Media Player")]
    public void IsLikelyMusicApp_RecognizesCommonPlayers(string appId, string displayName)
    {
        var app = CreateApp(appId, displayName);

        Assert.True(CenterCardMediaSnapshotProvider.IsLikelyMusicApp(app));
    }

    private static RunningAppEntry CreateApp(string appId, string displayName)
    {
        return new RunningAppEntry(
            AppId: appId,
            DisplayName: displayName,
            ExePath: null,
            IsRunning: true,
            IsFavorite: false,
            IsHiddenInCapsule: false,
            RepresentativeWindowHandle: 1);
    }

    private static CenterCardMediaSnapshot CreateMediaSnapshotWithTimeline(
        string title,
        string artist,
        string lyric,
        TimeSpan position,
        TimeSpan duration)
    {
        var constructor = typeof(CenterCardMediaSnapshot)
            .GetConstructors()
            .SingleOrDefault(candidate => candidate.GetParameters().Length == 8);
        Assert.NotNull(constructor);

        return (CenterCardMediaSnapshot)constructor!.Invoke([
            true,
            true,
            title,
            artist,
            lyric,
            "cloudmusic",
            position,
            duration
        ]);
    }

    private static object? GetRequiredProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return property!.GetValue(instance);
    }
}
