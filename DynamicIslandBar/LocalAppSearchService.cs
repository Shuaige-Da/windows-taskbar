using System.IO;
using Microsoft.Win32;

namespace DynamicIslandBar;

public sealed record LocalInstalledApp(string AppId, string DisplayName, string? LaunchPath);

public static class LocalAppSearchService
{
    private const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<LocalInstalledApp> EnumerateInstalledApps()
    {
        var apps = new Dictionary<string, LocalInstalledApp>(StringComparer.OrdinalIgnoreCase);

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (var app in EnumerateInstalledApps(hive, view))
                {
                    if (string.IsNullOrWhiteSpace(app.DisplayName))
                    {
                        continue;
                    }

                    if (apps.TryGetValue(app.AppId, out var existing)
                        && !string.IsNullOrWhiteSpace(existing.LaunchPath))
                    {
                        continue;
                    }

                    apps[app.AppId] = app;
                }
            }
        }

        return apps.Values
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IEnumerable<LocalInstalledApp> Search(IEnumerable<LocalInstalledApp> apps, string query)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        return apps
            .Where(app => !string.IsNullOrWhiteSpace(app.DisplayName) && !string.IsNullOrWhiteSpace(app.LaunchPath))
            .Select(app => new
            {
                App = app,
                MatchIndex = app.DisplayName.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase)
            })
            .Where(item => item.MatchIndex >= 0)
            .OrderBy(item => item.MatchIndex == 0 ? 0 : 1)
            .ThenBy(item => item.MatchIndex)
            .ThenBy(item => item.App.DisplayName.Length)
            .ThenBy(item => item.App.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.App);
    }

    private static IEnumerable<LocalInstalledApp> EnumerateInstalledApps(RegistryHive hive, RegistryView view)
    {
        RegistryKey? uninstallRoot = null;
        try
        {
            uninstallRoot = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(UninstallRegistryPath);
        }
        catch
        {
            yield break;
        }

        if (uninstallRoot == null)
        {
            yield break;
        }

        using (uninstallRoot)
        {
            foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
            {
                RegistryKey? appKey = null;
                try
                {
                    appKey = uninstallRoot.OpenSubKey(subKeyName);
                }
                catch
                {
                }

                if (appKey == null)
                {
                    continue;
                }

                using (appKey)
                {
                    var displayName = appKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    var launchPath = ResolveLaunchPath(appKey, displayName);
                    var appId = BuildAppId(displayName, launchPath, subKeyName);
                    yield return new LocalInstalledApp(appId, displayName.Trim(), launchPath);
                }
            }
        }
    }

    private static string BuildAppId(string displayName, string? launchPath, string registryKeyName)
    {
        if (!string.IsNullOrWhiteSpace(launchPath))
        {
            return launchPath;
        }

        return $"{displayName.Trim()}::{registryKeyName}";
    }

    private static string? ResolveLaunchPath(RegistryKey appKey, string displayName)
    {
        var displayIcon = appKey.GetValue("DisplayIcon") as string;
        var launchFromIcon = ExtractExecutablePath(displayIcon);
        if (!string.IsNullOrWhiteSpace(launchFromIcon))
        {
            return launchFromIcon;
        }

        var installLocation = appKey.GetValue("InstallLocation") as string;
        var launchFromInstallLocation = ResolveExecutableFromInstallLocation(installLocation, displayName);
        if (!string.IsNullOrWhiteSpace(launchFromInstallLocation))
        {
            return launchFromInstallLocation;
        }

        var uninstallString = appKey.GetValue("UninstallString") as string;
        var launchFromUninstallString = ExtractExecutablePath(uninstallString);
        if (!string.IsNullOrWhiteSpace(launchFromUninstallString)
            && !IsSystemMaintenanceExecutable(launchFromUninstallString))
        {
            return launchFromUninstallString;
        }

        return null;
    }

    private static string? ExtractExecutablePath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var normalized = Environment.ExpandEnvironmentVariables(candidate.Trim());
        if (normalized.StartsWith('"'))
        {
            var endQuote = normalized.IndexOf('"', 1);
            if (endQuote > 1)
            {
                normalized = normalized[1..endQuote];
            }
        }
        else
        {
            var exeIndex = normalized.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                normalized = normalized[..(exeIndex + 4)];
            }
        }

        normalized = normalized.Trim().Trim('"');
        if (!normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Path.IsPathRooted(normalized) || !File.Exists(normalized))
        {
            return null;
        }

        return normalized;
    }

    private static string? ResolveExecutableFromInstallLocation(string? installLocation, string displayName)
    {
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return null;
        }

        var normalizedPath = Environment.ExpandEnvironmentVariables(installLocation.Trim());
        if (!Directory.Exists(normalizedPath))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(normalizedPath, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(path => !IsSystemMaintenanceExecutable(path))
                .OrderBy(path => ScoreExecutable(path, displayName))
                .ThenBy(path => path.Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static int ScoreExecutable(string executablePath, string displayName)
    {
        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.Equals(fileName, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (fileName.Contains(displayName, StringComparison.OrdinalIgnoreCase)
            || displayName.Contains(fileName, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static bool IsSystemMaintenanceExecutable(string executablePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        return fileName.Contains("unins", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("uninstall", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("setup", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("update", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("repair", StringComparison.OrdinalIgnoreCase);
    }
}
