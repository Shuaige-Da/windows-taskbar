using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class RunningAppsRefreshPolicyTests
{
    [Fact]
    public void GetInterval_UsesFastRefreshDuringInteraction()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), RunningAppsRefreshPolicy.GetInterval(isInteractive: true));
    }

    [Fact]
    public void GetInterval_UsesSlowerRefreshWhileIdle()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), RunningAppsRefreshPolicy.GetInterval(isInteractive: false));
    }

    [Fact]
    public void RequiresUiRefresh_ReturnsFalseForEquivalentSnapshots()
    {
        var entry = CreateEntry("app.exe");
        var previous = new RunningAppsSnapshot([entry], [entry], [], true);
        var current = new RunningAppsSnapshot([entry with { }], [entry with { }], [], true);

        Assert.False(RunningAppsRefreshPolicy.RequiresUiRefresh(previous, current));
    }

    [Fact]
    public void RequiresUiRefresh_DetectsChangedWindowStateAndLayout()
    {
        var entry = CreateEntry("app.exe");
        var previous = new RunningAppsSnapshot([entry], [entry], [], true);
        var foregroundChanged = new RunningAppsSnapshot(
            [entry with { IsForeground = true }],
            [entry with { IsForeground = true }],
            [],
            true);
        var layoutChanged = new RunningAppsSnapshot([entry], [], [entry], true);

        Assert.True(RunningAppsRefreshPolicy.RequiresUiRefresh(previous, foregroundChanged));
        Assert.True(RunningAppsRefreshPolicy.RequiresUiRefresh(previous, layoutChanged));
    }

    private static RunningAppEntry CreateEntry(string appId)
    {
        return new RunningAppEntry(
            appId,
            appId,
            appId,
            IsRunning: true,
            IsFavorite: false,
            IsHiddenInCapsule: false,
            RepresentativeWindowHandle: 1);
    }
}
