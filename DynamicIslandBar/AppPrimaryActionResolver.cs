namespace DynamicIslandBar;

public enum AppPrimaryAction
{
    ActivateOrLaunch,
    Minimize
}

public static class AppPrimaryActionResolver
{
    public static AppPrimaryAction Resolve(RunningAppEntry app, string? recentlyActivatedAppId)
    {
        if (!app.IsRunning)
        {
            return AppPrimaryAction.ActivateOrLaunch;
        }

        if (app.IsForeground
            || string.Equals(app.AppId, recentlyActivatedAppId, StringComparison.OrdinalIgnoreCase))
        {
            return AppPrimaryAction.Minimize;
        }

        return AppPrimaryAction.ActivateOrLaunch;
    }
}
