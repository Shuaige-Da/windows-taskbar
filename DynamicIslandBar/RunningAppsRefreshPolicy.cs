namespace DynamicIslandBar;

public static class RunningAppsRefreshPolicy
{
    public static TimeSpan GetInterval(bool isInteractive)
    {
        return isInteractive ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(3);
    }
}
