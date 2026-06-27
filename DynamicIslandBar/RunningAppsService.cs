using System.IO;

namespace DynamicIslandBar;

public sealed record WindowAppCandidate(
    string Title,
    string AppId,
    nint WindowHandle,
    bool IsForeground,
    string? ExePath = null,
    int ProcessId = 0);

public sealed record RunningAppEntry(
    string AppId,
    string DisplayName,
    string? ExePath,
    bool IsRunning,
    bool IsFavorite,
    bool IsHiddenInCapsule,
    nint RepresentativeWindowHandle,
    int RepresentativeProcessId = 0,
    bool IsForeground = false);

public sealed record RunningAppsSnapshot(
    IReadOnlyList<RunningAppEntry> AllApps,
    IReadOnlyList<RunningAppEntry> MainBarApps,
    IReadOnlyList<RunningAppEntry> OverflowApps,
    bool HasOverflowFolder);

public sealed record AppsMenuState(
    bool CanOpenApp,
    bool CanCloseApp,
    bool CanToggleFavorite,
    bool CanShowInCapsule,
    bool CanHideFromCapsule);

public static class AppsMenuStateBuilder
{
    public static AppsMenuState Build(RunningAppEntry entry)
    {
        return new AppsMenuState(
            CanOpenApp: !entry.IsRunning && !string.IsNullOrWhiteSpace(entry.ExePath),
            CanCloseApp: entry.IsRunning,
            CanToggleFavorite: true,
            CanShowInCapsule: entry.IsHiddenInCapsule,
            CanHideFromCapsule: !entry.IsHiddenInCapsule);
    }
}

public static class RunningAppsSnapshotBuilder
{
    public static RunningAppsSnapshot Build(
        IReadOnlyList<WindowAppCandidate> candidates,
        CapsuleConfig config,
        int visibleSlots)
    {
        var runningApps = candidates
            .GroupBy(candidate => candidate.AppId)
            .Select(group => new RunningAppEntry(
                AppId: group.Key,
                DisplayName: group.First().Title,
                ExePath: group.First().ExePath
                    ?? (config.KnownLaunchPaths.TryGetValue(group.Key, out var path) ? path : group.Key),
                IsRunning: true,
                IsFavorite: config.FavoriteApps.Contains(group.Key),
                IsHiddenInCapsule: config.HiddenApps.Contains(group.Key),
                RepresentativeWindowHandle: group.First().WindowHandle,
                RepresentativeProcessId: group.First().ProcessId,
                IsForeground: group.Any(candidate => candidate.IsForeground)))
            .ToList();

        var apps = new List<RunningAppEntry>(runningApps);
        foreach (var favoriteAppId in config.FavoriteApps)
        {
            if (apps.Any(app => app.AppId == favoriteAppId))
            {
                continue;
            }

            apps.Add(new RunningAppEntry(
                AppId: favoriteAppId,
                DisplayName: GetDisplayName(favoriteAppId),
                ExePath: config.KnownLaunchPaths.TryGetValue(favoriteAppId, out var path) ? path : null,
                IsRunning: false,
                IsFavorite: true,
                IsHiddenInCapsule: config.HiddenApps.Contains(favoriteAppId),
                RepresentativeWindowHandle: 0,
                RepresentativeProcessId: 0,
                IsForeground: false));
        }

        apps = apps
            .OrderByDescending(app => app.IsRunning)
            .ThenBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var visibleApps = apps.Where(app => !app.IsHiddenInCapsule).ToList();
        var mainBarCapacity = visibleApps.Count > visibleSlots
            ? Math.Max(visibleSlots - 1, 0)
            : visibleSlots;
        var mainBarApps = visibleApps.Take(mainBarCapacity).ToList();
        var overflowApps = visibleApps.Skip(mainBarCapacity).ToList();

        return new RunningAppsSnapshot(
            AllApps: apps,
            MainBarApps: mainBarApps,
            OverflowApps: overflowApps,
            HasOverflowFolder: overflowApps.Count > 0);
    }

    private static string GetDisplayName(string appId)
    {
        var fileName = Path.GetFileNameWithoutExtension(appId);
        return string.IsNullOrWhiteSpace(fileName) ? appId : fileName;
    }
}
