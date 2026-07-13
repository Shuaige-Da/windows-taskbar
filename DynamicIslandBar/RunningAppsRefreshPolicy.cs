namespace DynamicIslandBar;

public static class RunningAppsRefreshPolicy
{
    public static TimeSpan GetInterval(bool isInteractive)
    {
        return isInteractive ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(3);
    }

    public static bool RequiresUiRefresh(
        RunningAppsSnapshot previous,
        RunningAppsSnapshot current)
    {
        return previous.ShowAppLibrary != current.ShowAppLibrary
            || !previous.AllApps.SequenceEqual(current.AllApps)
            || !previous.MainBarApps.SequenceEqual(current.MainBarApps)
            || !previous.OverflowApps.SequenceEqual(current.OverflowApps);
    }
}
