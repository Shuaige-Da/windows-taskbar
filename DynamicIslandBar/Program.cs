namespace DynamicIslandBar;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (TaskbarRestoreWatchdog.TryGetParentProcessId(args, out var parentProcessId))
        {
            TaskbarRestoreWatchdog.RunUntilParentExits(parentProcessId);
            return;
        }

        StartupEnvironment.EnsureWindowsFontEnvironment();

        var app = new App();
        app.Run();
    }
}
