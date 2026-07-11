using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class LyricSearchMetadataPolicyTests
{
    [Fact]
    public void BuildIdentity_DerivesArtistFromSeparatedTitleWhenArtistIsMissing()
    {
        var identity = LyricSearchMetadataPolicy.BuildIdentity(
            "稻香 - 周杰伦",
            string.Empty,
            TimeSpan.FromSeconds(223));

        Assert.Equal("稻香", identity.Title);
        Assert.Equal("周杰伦", identity.Artist);
        Assert.Equal(TimeSpan.FromSeconds(223), identity.Duration);
        Assert.True(identity.ArtistWasDerivedFromTitle);
    }

    [Fact]
    public void BuildQueries_IncludesArtistQueryAndTitleOnlyFallback()
    {
        var identity = new LyricSearchIdentity("七里香", "周杰伦", TimeSpan.FromSeconds(299));

        var queries = LyricSearchMetadataPolicy.BuildQueries(identity);

        Assert.Contains(new LyricSearchQuery("七里香", "周杰伦"), queries);
        Assert.Contains(new LyricSearchQuery("七里香", string.Empty), queries);
    }

    [Fact]
    public void BuildQueries_IncludesCleanedTitleWhenVersionSuffixExists()
    {
        var identity = LyricSearchMetadataPolicy.BuildIdentity(
            "晴天 (Live版)",
            "周杰伦",
            TimeSpan.FromSeconds(269));

        var queries = LyricSearchMetadataPolicy.BuildQueries(identity);

        Assert.Contains(new LyricSearchQuery("晴天", "周杰伦"), queries);
        Assert.Contains(new LyricSearchQuery("晴天", string.Empty), queries);
    }

    [Fact]
    public void BuildQueries_IncludesPrimaryArtistForMultiArtistMetadata()
    {
        var identity = LyricSearchMetadataPolicy.BuildIdentity(
            "说好不哭",
            "周杰伦 / 五月天阿信",
            TimeSpan.FromSeconds(222));

        var queries = LyricSearchMetadataPolicy.BuildQueries(identity);

        Assert.Contains(new LyricSearchQuery("说好不哭", "周杰伦 / 五月天阿信"), queries);
        Assert.Contains(new LyricSearchQuery("说好不哭", "周杰伦"), queries);
    }

    [Fact]
    public void BuildQueries_IncludesReversedFallbackForAmbiguousCombinedMetadata()
    {
        var identity = LyricSearchMetadataPolicy.BuildIdentity(
            "周杰伦 - 稻香",
            string.Empty,
            TimeSpan.FromSeconds(223));

        var queries = LyricSearchMetadataPolicy.BuildQueries(identity);

        Assert.Contains(new LyricSearchQuery("稻香", "周杰伦"), queries);
        Assert.Contains(new LyricSearchQuery("稻香", string.Empty), queries);
    }

    [Fact]
    public void BuildIdentity_RemovesFeaturingAndOfficialVideoSuffixes()
    {
        var featuring = LyricSearchMetadataPolicy.BuildIdentity(
            "Stay feat. Guest",
            "Singer",
            TimeSpan.Zero);
        var officialVideo = LyricSearchMetadataPolicy.BuildIdentity(
            "晴天 - Official Music Video",
            "周杰伦",
            TimeSpan.Zero);

        Assert.Equal("Stay", featuring.Title);
        Assert.Equal("晴天", officialVideo.Title);
    }
}
