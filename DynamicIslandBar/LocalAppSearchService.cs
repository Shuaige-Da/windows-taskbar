using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DynamicIslandBar;

public readonly record struct LocalAppEntry(
    string DisplayName,
    string? ExecutablePath,
    string? IconPath);

public static class LocalAppSearchService
{
    private static readonly string[] RegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    private static List<LocalAppEntry>? _cachedApps;
    private static DateTime _cacheTimestamp;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public static List<LocalAppEntry> GetAllInstalledApps()
    {
        if (_cachedApps != null && DateTime.Now - _cacheTimestamp < CacheExpiry)
        {
            return _cachedApps;
        }

        var apps = new Dictionary<string, LocalAppEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var registryPath in RegistryPaths)
        {
            CollectAppsFromRegistry(RegistryHive.LocalMachine, registryPath, apps);
            CollectAppsFromRegistry(RegistryHive.CurrentUser, registryPath, apps);
        }

        _cachedApps = apps.Values
            .Where(a => !string.IsNullOrWhiteSpace(a.DisplayName))
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _cacheTimestamp = DateTime.Now;

        return _cachedApps;
    }

    public static List<LocalAppEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();
        return GetAllInstalledApps()
            .Where(a => a.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();
    }

    private static void CollectAppsFromRegistry(
        RegistryHive hive,
        string registryPath,
        Dictionary<string, LocalAppEntry> apps)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var uninstallKey = baseKey.OpenSubKey(registryPath);
            if (uninstallKey == null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    if (subKey == null)
                    {
                        continue;
                    }

                    var displayName = subKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    var installLocation = subKey.GetValue("InstallLocation") as string;
                    var displayIcon = subKey.GetValue("DisplayIcon") as string;

                    string? executablePath = null;
                    if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                    {
                        executablePath = TryFindExecutable(installLocation, displayName);
                    }

                    if (string.IsNullOrWhiteSpace(executablePath) && !string.IsNullOrWhiteSpace(displayIcon))
                    {
                        var iconPath = displayIcon.Split(',')[0].Trim('"').Trim();
                        if (File.Exists(iconPath))
                        {
                            executablePath = iconPath;
                        }
                    }

                    if (!apps.ContainsKey(displayName!))
                    {
                        apps[displayName!] = new LocalAppEntry(
                            displayName!,
                            executablePath,
                            string.IsNullOrWhiteSpace(displayIcon) ? null : displayIcon.Split(',')[0].Trim('"').Trim());
                    }
                }
                catch
                {
                    // Skip inaccessible registry keys
                }
            }
        }
        catch
        {
            // Skip inaccessible registry paths
        }
    }

    private static string? TryFindExecutable(string installLocation, string displayName)
    {
        try
        {
            var dir = new DirectoryInfo(installLocation);
            var exeFiles = dir.GetFiles("*.exe", SearchOption.TopDirectoryOnly);
            if (exeFiles.Length == 0)
            {
                return null;
            }

            var normalizedName = Path.GetFileNameWithoutExtension(displayName)
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "");

            foreach (var exe in exeFiles)
            {
                var exeName = Path.GetFileNameWithoutExtension(exe.Name)
                    .Replace(" ", "")
                    .Replace("-", "")
                    .Replace("_", "");

                if (normalizedName.Contains(exeName, StringComparison.OrdinalIgnoreCase) ||
                    exeName.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    return exe.FullName;
                }
            }

            return exeFiles[0].FullName;
        }
        catch
        {
            return null;
        }
    }

    public static void LaunchApp(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Launch failed - best effort only
        }
    }
}
