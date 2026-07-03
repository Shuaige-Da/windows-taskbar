namespace DynamicIslandBar.Tests;

public class LyricsServiceTests
{
    [Fact]
    public async Task ResolveCurrentLyricAsync_UsesLrcAndPlaybackPosition()
    {
        var provider = new FakeLyricsProvider(
            """
            [00:10.00]第一句歌词
            [00:12.30]我在黄昏里等风经过
            """);
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "像鱼",
            Artist: "王贰浪",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(12.5),
            Duration: TimeSpan.FromSeconds(300));

        var resolved = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("我在黄昏里等风经过", resolved.Lyric);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_UsesMergedTranslatedLrcAtPlaybackPosition()
    {
        var provider = new FakeLyricsProvider(
            """
            [01:25.343]So don't try to stop me now / 我已无法停止
            [01:41.137]So don't try to stop me now / 我已无法止步
            [01:45.147]No don't try to stop me now / 我已无所可敌
            """);
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "Moments",
            Artist: "Leo Stannard / Kidnap",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(102),
            Duration: TimeSpan.FromSeconds(304));

        var resolved = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("So don't try to stop me now / 我已无法止步", resolved.Lyric);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_CachesLrcForSameSong()
    {
        var provider = new FakeLyricsProvider(
            """
            [00:01.00]第一句歌词
            [00:02.00]第二句歌词
            """);
        var service = new LyricsService(provider);
        var first = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "像鱼",
            Artist: "王贰浪",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(1),
            Duration: TimeSpan.FromSeconds(300));
        var second = first with { Position = TimeSpan.FromSeconds(2.2) };

        Assert.Equal("第一句歌词", (await service.ResolveCurrentLyricAsync(first)).Lyric);
        Assert.Equal("第二句歌词", (await service.ResolveCurrentLyricAsync(second)).Lyric);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_KeepsCacheWhenEstimatedDurationIsFedBack()
    {
        var provider = new SequenceLyricsProvider(
            """
            [00:01.00]第一句歌词
            [00:12.00]估算时长后的歌词
            """,
            null);
        var service = new LyricsService(provider, negativeCacheDuration: TimeSpan.Zero);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "是我不够好",
            Artist: "李毓芬",
            Lyric: string.Empty);

        var first = await service.ResolveCurrentLyricAsync(snapshot);
        var second = await service.ResolveCurrentLyricAsync(first with { Position = TimeSpan.FromSeconds(12.5) });

        Assert.Equal("估算时长后的歌词", second.Lyric);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_SharesConcurrentLoadsForSameSong()
    {
        var provider = new ConcurrentLyricsProvider(
            "[00:01.00]共享请求的歌词");
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "我是真的爱上你",
            Artist: "王杰",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(1.5));

        var firstResolve = service.ResolveCurrentLyricAsync(snapshot);
        await provider.WaitForCallCountAsync(1);
        var secondResolve = service.ResolveCurrentLyricAsync(snapshot);
        await Task.Delay(50);

        provider.CompleteCall(0);
        Assert.Equal("共享请求的歌词", (await firstResolve).Lyric);
        Assert.Equal("共享请求的歌词", (await secondResolve).Lyric);

        var resolvedAgain = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("共享请求的歌词", resolvedAgain.Lyric);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_RetriesWhenProviderReturnsNoLrc()
    {
        var provider = new SequenceLyricsProvider(
            null,
            "[00:01.00]重试后的歌词");
        var service = new LyricsService(provider, negativeCacheDuration: TimeSpan.Zero);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "像鱼",
            Artist: "王贰浪",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(1.5),
            Duration: TimeSpan.FromSeconds(300));

        Assert.Equal("暂无歌词", (await service.ResolveCurrentLyricAsync(snapshot)).Lyric);
        Assert.Equal("重试后的歌词", (await service.ResolveCurrentLyricAsync(snapshot)).Lyric);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_RetriesProviderWithinSameResolveWhenConfigured()
    {
        var provider = new SequenceLyricsProvider(
            null,
            "[00:01.00]同一次刷新拿到歌词");
        var service = new LyricsService(provider, loadRetryCount: 1, loadRetryDelay: TimeSpan.Zero);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "红豆",
            Artist: "王菲",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(1.5),
            Duration: TimeSpan.FromSeconds(256));

        var resolved = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("同一次刷新拿到歌词", resolved.Lyric);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_NegativeCachesMissingLyrics()
    {
        var provider = new SequenceLyricsProvider(null, "[00:01.00]不应该马上重复请求");
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "没有歌词的歌",
            Artist: "未知歌手",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(1.5),
            Duration: TimeSpan.FromSeconds(300));

        Assert.Equal("暂无歌词", (await service.ResolveCurrentLyricAsync(snapshot)).Lyric);
        Assert.Equal("暂无歌词", (await service.ResolveCurrentLyricAsync(snapshot)).Lyric);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_CleansTitleBeforeProviderSearch()
    {
        var provider = new FakeLyricsProvider("[00:01.00]Moved to the city in a broke down car and");
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "Closer (VIP) - The Chainsmokers",
            Artist: "The Chainsmokers / Halsey",
            Lyric: string.Empty,
            Position: TimeSpan.FromSeconds(1.5),
            Duration: TimeSpan.FromSeconds(244));

        await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("Closer", provider.LastTitle);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_FetchesLyricsWithoutTimeline()
    {
        var provider = new FakeLyricsProvider(
            """
            [00:00.00] 作词 : 小寒
            [00:17.03]欢笑声 欢呼声
            """);
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "孤独患者",
            Artist: "陈奕迅",
            Lyric: string.Empty);

        var resolved = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("欢笑声 欢呼声", resolved.Lyric);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_SkipsProductionCreditsWithoutTimeline()
    {
        var provider = new FakeLyricsProvider(
            """
            [00:00.00] 作词 : 法老/KKECHO
            [00:02.00] 编曲 : Land James
            [00:06.00] 录音：杨秋儒
            [00:09.00] 混音：隆历奇
            [00:15.00] 王以太：
            [00:18.49] 我从出生就是一个自闭小孩
            """);
        var service = new LyricsService(provider);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "会魔法的老人",
            Artist: "法老",
            Lyric: string.Empty);

        var resolved = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("我从出生就是一个自闭小孩", resolved.Lyric);
    }

    [Fact]
    public async Task ResolveCurrentLyricAsync_EstimatesProgressWhenTimelineIsMissing()
    {
        var now = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var provider = new FakeLyricsProvider(
            """
            [00:00.00]For You
            [00:02.00]都存在你目光
            [00:05.00]是汹涌澎湃
            """);
        var service = new LyricsService(provider, utcNow: () => now);
        var snapshot = new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "For You",
            Artist: "Fansail_10",
            Lyric: string.Empty);

        var first = await service.ResolveCurrentLyricAsync(snapshot);
        now = now.AddSeconds(5.2);
        var second = await service.ResolveCurrentLyricAsync(snapshot);

        Assert.Equal("都存在你目光", first.Lyric);
        Assert.Equal("是汹涌澎湃", second.Lyric);
        Assert.Equal(TimeSpan.Zero, first.Position);
        Assert.True(second.Position >= TimeSpan.FromSeconds(5));
        Assert.True(second.Duration >= TimeSpan.FromSeconds(13));
    }

    [Fact]
    public async Task CompositeLyricsProvider_ContinuesWhenEarlierProviderThrows()
    {
        var provider = new CompositeLyricsProvider(
            new ThrowingLyricsProvider(),
            new FakeLyricsProvider("[00:01.00]后续来源歌词"));

        var lrc = await provider.TryGetLrcAsync("任意歌", "任意歌手", null);

        Assert.Equal("[00:01.00]后续来源歌词", lrc);
    }

    private sealed class FakeLyricsProvider(string? lrc) : ILyricsProvider
    {
        public int CallCount { get; private set; }
        public string? LastTitle { get; private set; }

        public Task<string?> TryGetLrcAsync(
            string title,
            string artist,
            TimeSpan? duration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTitle = title;
            return Task.FromResult(lrc);
        }
    }

    private sealed class SequenceLyricsProvider(params string?[] lrcResponses) : ILyricsProvider
    {
        public int CallCount { get; private set; }

        public Task<string?> TryGetLrcAsync(
            string title,
            string artist,
            TimeSpan? duration,
            CancellationToken cancellationToken = default)
        {
            var response = CallCount < lrcResponses.Length
                ? lrcResponses[CallCount]
                : lrcResponses.LastOrDefault();
            CallCount++;
            return Task.FromResult(response);
        }
    }

    private sealed class ConcurrentLyricsProvider(params string?[] lrcResponses) : ILyricsProvider
    {
        private readonly TaskCompletionSource[] _releaseSignals = lrcResponses
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously))
            .ToArray();
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<string?> TryGetLrcAsync(
            string title,
            string artist,
            TimeSpan? duration,
            CancellationToken cancellationToken = default)
        {
            var callIndex = Interlocked.Increment(ref _callCount) - 1;
            await _releaseSignals[Math.Min(callIndex, _releaseSignals.Length - 1)].Task;
            return callIndex < lrcResponses.Length
                ? lrcResponses[callIndex]
                : lrcResponses.LastOrDefault();
        }

        public void CompleteCall(int callIndex)
        {
            _releaseSignals[callIndex].TrySetResult();
        }

        public async Task WaitForCallCountAsync(int expectedCallCount)
        {
            while (Volatile.Read(ref _callCount) < expectedCallCount)
            {
                await Task.Delay(10);
            }
        }
    }

    private sealed class ThrowingLyricsProvider : ILyricsProvider
    {
        public Task<string?> TryGetLrcAsync(
            string title,
            string artist,
            TimeSpan? duration,
            CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("first source timed out");
        }
    }
}
