using System.IO;

namespace DynamicIslandBar;

public sealed record WindowAppCandidate(
    string Title,
    string AppId,
    nint WindowHandle,
    bool IsForeground,
    string? ExePath = null,
    int ProcessId = 0,
    bool IsProcessMainWindow = false,
    bool IsToolWindow = false,
    bool IsNoActivateWindow = false,
    bool IsOwnedWindow = false,
    long WindowArea = 0);

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
    bool ShowAppLibrary);

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
            .Select(group =>
            {
                var representative = SelectRepresentativeWindow(group);
                return new RunningAppEntry(
                    AppId: group.Key,
                    DisplayName: representative.Title,
                    ExePath: representative.ExePath
                        ?? (config.KnownLaunchPaths.TryGetValue(group.Key, out var path) ? path : group.Key),
                    IsRunning: true,
                    IsFavorite: config.FavoriteApps.Contains(group.Key),
                    IsHiddenInCapsule: config.HiddenApps.Contains(group.Key),
                    RepresentativeWindowHandle: representative.WindowHandle,
                    RepresentativeProcessId: representative.ProcessId,
                    IsForeground: representative.IsForeground);
            })
            .ToList();

        var apps = new List<RunningAppEntry>(runningApps);
        var knownAppIds = new HashSet<string>(
            runningApps.Select(app => app.AppId),
            StringComparer.Ordinal);
        foreach (var favoriteAppId in config.FavoriteApps)
        {
            if (!knownAppIds.Add(favoriteAppId))
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
        // The app library is also the installed-app search entry, so it always owns one slot.
        var mainBarCapacity = Math.Max(visibleSlots - 1, 0);
        var mainBarApps = visibleApps.Take(mainBarCapacity).ToList();
        var overflowApps = visibleApps.Skip(mainBarCapacity).ToList();

        return new RunningAppsSnapshot(
            AllApps: apps,
            MainBarApps: mainBarApps,
            OverflowApps: overflowApps,
            ShowAppLibrary: true);
    }

    private static WindowAppCandidate SelectRepresentativeWindow(
        IEnumerable<WindowAppCandidate> candidates)
    {
        return candidates
            .OrderBy(candidate => candidate.IsToolWindow)
            .ThenBy(candidate => candidate.IsNoActivateWindow)
            .ThenBy(candidate => candidate.IsOwnedWindow)
            .ThenByDescending(candidate => candidate.IsProcessMainWindow)
            .ThenByDescending(candidate => candidate.WindowArea)
            .ThenByDescending(candidate => candidate.IsForeground)
            .First();
    }

    private static string GetDisplayName(string appId)
    {
        var fileName = Path.GetFileNameWithoutExtension(appId);
        return string.IsNullOrWhiteSpace(fileName) ? appId : fileName;
    }
}
