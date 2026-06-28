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
}
