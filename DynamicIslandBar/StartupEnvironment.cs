using System.IO;

namespace DynamicIslandBar;

public static class StartupEnvironment
{
    public static string? ResolveWindowsDirectory(string? windir, string? systemRoot)
    {
        foreach (var candidate in new[] { windir, systemRoot })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var trimmed = candidate.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.IsPathRooted(trimmed))
            {
                return trimmed;
            }
        }

        return null;
    }

    public static string? EnsureWindowsFontEnvironment()
    {
        var resolved = ResolveWindowsDirectory(
            Environment.GetEnvironmentVariable("windir"),
            Environment.GetEnvironmentVariable("SystemRoot"));

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            Environment.SetEnvironmentVariable("windir", resolved, EnvironmentVariableTarget.Process);
        }

        return resolved;
    }
}
