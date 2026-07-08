using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class LyricsFetchCoordinatorTests
{
    [Fact]
    public void BeginNewRequest_InvalidatesOlderGeneration()
    {
        var coordinator = new LyricsFetchCoordinator();

        var first = coordinator.Begin("SongA|ArtistA");
        var second = coordinator.Begin("SongB|ArtistB");

        Assert.False(coordinator.IsCurrent(first));
        Assert.True(coordinator.IsCurrent(second));
    }
}
