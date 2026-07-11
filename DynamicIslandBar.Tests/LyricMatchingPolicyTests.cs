using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class LyricMatchingPolicyTests
{
    [Fact]
    public void RankMetadataCandidates_ScoresBeforeLyricsAreDownloaded()
    {
        var identity = new LyricSearchIdentity("晴天", "周杰伦", TimeSpan.FromSeconds(269));
        var candidates = new[]
        {
            new LyricCandidate("netease", "wrong", "晴天 DJ", "其他歌手", TimeSpan.FromSeconds(180), false, false, false, false),
            new LyricCandidate("netease", "best", "晴天", "周杰伦", TimeSpan.FromSeconds(269), false, false, false, false)
        };

        var ranked = LyricMatchingPolicy.RankMetadataCandidates(identity, candidates, maximumCount: 3);

        Assert.Equal("best", Assert.Single(ranked).Id);
    }

    [Fact]
    public void SelectBestCandidate_PrefersExactArtistAndClosestDuration()
    {
        var identity = new LyricSearchIdentity("晴天", "周杰伦", TimeSpan.FromSeconds(269));
        var candidates = new[]
        {
            new LyricCandidate("netease", "1", "晴天", "某翻唱", TimeSpan.FromSeconds(266), true, false, false, false),
            new LyricCandidate("netease", "2", "晴天", "周杰伦", TimeSpan.FromSeconds(269), true, false, false, false),
            new LyricCandidate("netease", "3", "晴天 (Live)", "周杰伦", TimeSpan.FromSeconds(311), true, false, false, false)
        };

        var selected = LyricMatchingPolicy.SelectBestCandidate(identity, candidates);

        Assert.NotNull(selected);
        Assert.Equal("2", selected!.Id);
    }

    [Fact]
    public void SelectBestCandidate_RejectsNoLyricAndUncollectedCandidates()
    {
        var identity = new LyricSearchIdentity("夜曲", "周杰伦", TimeSpan.FromSeconds(234));
        var candidates = new[]
        {
            new LyricCandidate("netease", "1", "夜曲", "周杰伦", TimeSpan.FromSeconds(234), false, false, true, false),
            new LyricCandidate("netease", "2", "夜曲", "周杰伦", TimeSpan.FromSeconds(234), false, false, false, true)
        };

        var selected = LyricMatchingPolicy.SelectBestCandidate(identity, candidates);

        Assert.Null(selected);
    }

    [Fact]
    public void Score_AppliesVersionPenaltyToLiveAndInstrumentalVariants()
    {
        var identity = new LyricSearchIdentity("七里香", "周杰伦", TimeSpan.FromSeconds(299));
        var albumVersion = new LyricCandidate("netease", "1", "七里香", "周杰伦", TimeSpan.FromSeconds(299), true, false, false, false);
        var liveVersion = new LyricCandidate("netease", "2", "七里香 Live", "周杰伦", TimeSpan.FromSeconds(299), true, false, false, false);
        var instrumental = new LyricCandidate("netease", "3", "七里香 伴奏", "周杰伦", TimeSpan.FromSeconds(299), true, false, false, false);

        var albumScore = LyricMatchingPolicy.Score(identity, albumVersion);
        var liveScore = LyricMatchingPolicy.Score(identity, liveVersion);
        var instrumentalScore = LyricMatchingPolicy.Score(identity, instrumental);

        Assert.True(albumScore > liveScore);
        Assert.True(albumScore > instrumentalScore);
    }
}
