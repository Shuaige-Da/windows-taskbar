using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class LocalAppSearchServiceTests
{
    [Fact]
    public void Search_PrioritizesPrefixMatches()
    {
        var apps = new[]
        {
            new LocalInstalledApp("1", "Music Player", @"C:\Apps\Music.exe"),
            new LocalInstalledApp("2", "Best Music", @"C:\Apps\BestMusic.exe")
        };

        var results = LocalAppSearchService.Search(apps, "music").ToList();

        Assert.Equal("Music Player", results[0].DisplayName);
        Assert.Equal("Best Music", results[1].DisplayName);
    }

    [Fact]
    public void Search_ExcludesEntriesWithoutLaunchPath()
    {
        var apps = new[]
        {
            new LocalInstalledApp("1", "Music Player", null),
            new LocalInstalledApp("2", "Music Box", @"C:\Apps\MusicBox.exe")
        };

        var results = LocalAppSearchService.Search(apps, "music").ToList();

        Assert.Single(results);
        Assert.Equal("Music Box", results[0].DisplayName);
    }

    [Fact]
    public void Search_ReturnsEmptyForBlankQuery()
    {
        var apps = new[]
        {
            new LocalInstalledApp("1", "Music Player", @"C:\Apps\Music.exe")
        };

        Assert.Empty(LocalAppSearchService.Search(apps, ""));
    }
}
