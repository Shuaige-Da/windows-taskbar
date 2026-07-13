namespace DynamicIslandBar;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (TaskbarRestoreWatchdog.TryRestoreTaskbar(args))
        {
            return;
        }

        if (TaskbarRestoreWatchdog.TryGetParentProcessId(args, out var parentProcessId))
        {
            TaskbarRestoreWatchdog.RunUntilParentExits(parentProcessId);
            return;
        }

        StartupEnvironment.EnsureWindowsFontEnvironment();

        if (!SingleInstanceCoordinator.TryAcquire(out var singleInstance))
        {
            SingleInstanceCoordinator.SignalExistingInstance();
            return;
        }

        using (singleInstance)
        {
            var app = new App();
            app.AttachSingleInstance(singleInstance!);
            app.Run();
        }
    }
}
