using System.Diagnostics;

namespace DynamicIslandBar;

public static class TaskbarRestoreWatchdog
{
    private const string WatcherArgument = "--restore-taskbar-when-parent-exits";

    public static string BuildWatcherArguments(int parentProcessId)
    {
        return $"{WatcherArgument} {parentProcessId}";
    }

    public static bool TryGetParentProcessId(string[] args, out int parentProcessId)
    {
        parentProcessId = 0;
        return args.Length == 2 &&
               string.Equals(args[0], WatcherArgument, StringComparison.Ordinal) &&
               int.TryParse(args[1], out parentProcessId) &&
               parentProcessId > 0;
    }

    public static void StartForCurrentProcess()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = BuildWatcherArguments(Environment.ProcessId),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch
        {
        }
    }

    public static void RunUntilParentExits(int parentProcessId)
    {
        try
        {
            using var parent = Process.GetProcessById(parentProcessId);
            parent.WaitForExit();
        }
        catch
        {
        }
        finally
        {
            TaskbarManager.Show();
        }
    }
}
