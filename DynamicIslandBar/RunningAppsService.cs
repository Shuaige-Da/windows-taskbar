namespace DynamicIslandBar;

public sealed record WindowAppCandidate(
    string Title,
    string AppId,
    nint WindowHandle,
    bool IsForeground);

public sealed record RunningAppEntry(
    string AppId,
    string DisplayName,
    string? ExePath,
    bool IsRunning,
    bool IsFavorite,
    bool IsHiddenInCapsule,
    nint RepresentativeWindowHandle);

public sealed record RunningAppsSnapshot(
    IReadOnlyList<RunningAppEntry> AllApps,
    IReadOnlyList<RunningAppEntry> MainBarApps,
    IReadOnlyList<RunningAppEntry> OverflowApps,
    bool HasOverflowFolder);

public static class RunningAppsSnapshotBuilder
{
    public static RunningAppsSnapshot Build(
        IReadOnlyList<WindowAppCandidate> candidates,
        CapsuleConfig config,
        int visibleSlots)
    {
        var apps = candidates
            .GroupBy(candidate => candidate.AppId)
            .Select(group => new RunningAppEntry(
                AppId: group.Key,
                DisplayName: group.First().Title,
                ExePath: config.KnownLaunchPaths.TryGetValue(group.Key, out var path) ? path : group.Key,
                IsRunning: true,
                IsFavorite: config.FavoriteApps.Contains(group.Key),
                IsHiddenInCapsule: config.HiddenApps.Contains(group.Key),
                RepresentativeWindowHandle: group.First().WindowHandle))
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
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
}
