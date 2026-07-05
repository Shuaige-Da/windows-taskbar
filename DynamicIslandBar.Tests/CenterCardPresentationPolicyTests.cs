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
        Assert.Equal("歌词：Take Me Hand - DAISHI DANCE", state.SecondaryText);
        Assert.True(state.ShowTransportControls);
        Assert.False(state.ShowLyricsMarquee);
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
    public void Build_UsesSongAndArtistAsPrimaryTextForHoveredMusicDetails()
    {
        var app = CreateApp("cloudmusic", "网易云音乐");
        var media = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "Life's A Struggle",
            Artist: "宋岳庭",
            Lyric: "妈妈给我生命 现在让我自生自灭");

        var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: true);

        Assert.Equal(CenterCardDisplayMode.MusicDetails, state.Mode);
        Assert.Equal("Life's A Struggle - 宋岳庭", state.PrimaryText);
        Assert.Equal("歌词：妈妈给我生命 现在让我自生自灭", state.SecondaryText);
        Assert.True(state.ShowTransportControls);
    }

    [Fact]
    public void Build_UsesLyricsMarqueeWhenLyricExistsEvenIfPlaybackFlagLags()
    {
        var app = CreateApp("cloudmusic", "网易云音乐");
        var media = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: false,
            Title: "Life's A Struggle",
            Artist: "宋岳庭",
            Lyric: "妈妈给我生命 现在让我自生自灭");

        var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: false);

        Assert.Equal(CenterCardDisplayMode.MusicLyricsMarquee, state.Mode);
        Assert.Equal("妈妈给我生命 现在让我自生自灭", state.PrimaryText);
        Assert.True(state.ShowLyricsMarquee);
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
    public void Resolve_IgnoresLiveSnapshotFromAnotherApp()
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

        Assert.NotNull(snapshot);
        Assert.NotEqual("浏览器声音", snapshot!.Title);
    }

    [Fact]
    public void SourceLooksLikeApp_MatchesExeAndDisplayName()
    {
        var app = CreateApp("d:\\qqmusic\\qqmusic.exe", "QQ音乐");

        Assert.True(CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, "QQMusic"));
        Assert.True(CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, "QQ音乐"));
        Assert.False(CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, "Chrome"));
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
}
