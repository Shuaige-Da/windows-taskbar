# DynamicIslandBar Extensible Shell And Running Apps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first extensible-shell version of `DynamicIslandBar` by introducing layout/theme/config foundations and then implementing live running-app icons, overflow management, and top/bottom capsule modes on top of that foundation.

**Architecture:** Keep `MainWindow` as the shell host, but move policy and state into focused helpers: `CapsuleConfigService` for persistence, `CapsuleLayoutManager` for placement and metrics, `CapsuleThemeManager` for presets, and `RunningAppsService` for app discovery and command handling. Use TDD for each non-trivial helper first, then wire the tested helpers into the WPF shell incrementally.

**Tech Stack:** C#, WPF on `net10.0-windows`, xUnit, Win32 interop in `WindowManager`, JSON persistence via `System.Text.Json`.

---

## File Map

### New production files

- `DynamicIslandBar/CapsuleConfigService.cs`
  Central persistence for capsule mode, theme preset, hidden apps, favorites, and launch paths.

- `DynamicIslandBar/CapsuleLayoutManager.cs`
  Mode, placement, metrics, popup direction, and visible-capacity calculations.

- `DynamicIslandBar/CapsuleThemeManager.cs`
  Theme preset definitions and theme values used by WPF shell styling.

- `DynamicIslandBar/RunningAppsService.cs`
  Composes app entries from low-level window/process data and merges persisted state.

### Existing production files to modify

- `DynamicIslandBar/WindowManager.cs`
  Expand low-level metadata and window actions needed by running-app behavior.

- `DynamicIslandBar/MainWindow.xaml`
  Replace demo dock with dynamic app host, overflow host, and mode-aware shell containers.

- `DynamicIslandBar/MainWindow.xaml.cs`
  Orchestrate config/layout/theme/running-app services, hover popups, context menus, and drag-snap behavior.

### New test files

- `DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs`
- `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
- `DynamicIslandBar.Tests/CapsuleThemeManagerTests.cs`
- `DynamicIslandBar.Tests/RunningAppsServiceTests.cs`

### Existing test files to keep

- `DynamicIslandBar.Tests/ServiceHelpersTests.cs`

### Verification note

- Before any `dotnet test` or `dotnet build` command that targets the app project, close any running `DynamicIslandBar.exe` instance. The current baseline shows the debug executable can be file-locked while the app is open.

## Task 1: Add Capsule Config Persistence

**Files:**
- Create: `DynamicIslandBar/CapsuleConfigService.cs`
- Create: `DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleConfigServiceTests
{
    [Fact]
    public void Deserialize_ReturnsDefaults_WhenJsonIsEmpty()
    {
        var config = CapsuleConfigSerializer.Deserialize("{}");

        Assert.Equal(CapsuleMode.BottomTaskbar, config.Mode);
        Assert.Equal(CapsuleThemePreset.ClassicDark, config.ThemePreset);
        Assert.Empty(config.FavoriteApps);
        Assert.Empty(config.HiddenApps);
        Assert.Empty(config.KnownLaunchPaths);
    }

    [Fact]
    public void MergeKnownLaunchPath_StoresNormalizedAppIdAndPath()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetKnownLaunchPath(
            config,
            @"c:\apps\wechat.exe",
            @"C:\Apps\WeChat.exe");

        Assert.Equal(
            @"C:\Apps\WeChat.exe",
            config.KnownLaunchPaths[@"c:\apps\wechat.exe"]);
    }

    [Fact]
    public void ToggleFavorite_AddsThenRemovesAppId()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetFavorite(config, "wechat", true);
        CapsuleConfigMutator.SetFavorite(config, "wechat", false);

        Assert.DoesNotContain("wechat", config.FavoriteApps);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleConfigServiceTests" --no-restore`

Expected: FAIL because `CapsuleConfig`, `CapsuleConfigSerializer`, `CapsuleConfigMutator`, `CapsuleMode`, and `CapsuleThemePreset` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Text.Json;

namespace DynamicIslandBar;

public enum CapsuleMode
{
    BottomTaskbar,
    TopIsland
}

public enum CapsuleThemePreset
{
    ClassicDark,
    GlassGreen,
    SoftLight
}

public sealed class CapsuleConfig
{
    public CapsuleMode Mode { get; set; } = CapsuleMode.BottomTaskbar;
    public CapsuleThemePreset ThemePreset { get; set; } = CapsuleThemePreset.ClassicDark;
    public HashSet<string> FavoriteApps { get; } = [];
    public HashSet<string> HiddenApps { get; } = [];
    public Dictionary<string, string> KnownLaunchPaths { get; } = [];
    public string? BackgroundImagePath { get; set; }
    public double BackgroundImageOpacity { get; set; }
    public string? BackgroundImageStretchMode { get; set; }
}

public static class CapsuleConfigSerializer
{
    public static CapsuleConfig Deserialize(string json)
    {
        var model = JsonSerializer.Deserialize<CapsuleConfigStore>(json) ?? new CapsuleConfigStore();
        return model.ToConfig();
    }
}

public static class CapsuleConfigMutator
{
    public static void SetFavorite(CapsuleConfig config, string appId, bool isFavorite)
    {
        if (isFavorite)
        {
            config.FavoriteApps.Add(appId);
            return;
        }

        config.FavoriteApps.Remove(appId);
    }

    public static void SetKnownLaunchPath(CapsuleConfig config, string appId, string path)
    {
        config.KnownLaunchPaths[appId] = path;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleConfigServiceTests" --no-restore`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CapsuleConfigService.cs DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs
git commit -m "feat: add capsule config foundation"
```

## Task 2: Add Layout Mode And Metrics Manager

**Files:**
- Create: `DynamicIslandBar/CapsuleLayoutManager.cs`
- Create: `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
- Modify: `DynamicIslandBar/CapsuleConfigService.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleLayoutManagerTests
{
    [Fact]
    public void GetMetrics_ReturnsWiderCapacityForBottomMode()
    {
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

        Assert.True(bottom.CapsuleWidth > top.CapsuleWidth);
        Assert.True(bottom.VisibleAppSlots > top.VisibleAppSlots);
        Assert.Equal(PopupFlowDirection.Up, bottom.PopupDirection);
        Assert.Equal(PopupFlowDirection.Down, top.PopupDirection);
    }

    [Fact]
    public void ResolveDropMode_SnapsToTopWhenCloseToTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenHeight: 1080,
            topAfterDrag: 20,
            currentMode: CapsuleMode.BottomTaskbar);

        Assert.Equal(CapsuleMode.TopIsland, mode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleLayoutManagerTests" --no-restore`

Expected: FAIL because `CapsuleLayoutManager`, `PopupFlowDirection`, and `LayoutMetrics` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace DynamicIslandBar;

public enum PopupFlowDirection
{
    Up,
    Down
}

public readonly record struct LayoutMetrics(
    double CapsuleWidth,
    double CapsuleHeight,
    int VisibleAppSlots,
    PopupFlowDirection PopupDirection);

public static class CapsuleLayoutManager
{
    public static LayoutMetrics GetMetrics(CapsuleMode mode, double screenWidth, double screenHeight)
    {
        return mode switch
        {
            CapsuleMode.TopIsland => new LayoutMetrics(760, 64, 5, PopupFlowDirection.Down),
            _ => new LayoutMetrics(Math.Min(screenWidth - 120, 1380), 72, 12, PopupFlowDirection.Up)
        };
    }

    public static CapsuleMode ResolveDropMode(double screenHeight, double topAfterDrag, CapsuleMode currentMode)
    {
        if (topAfterDrag <= 72)
        {
            return CapsuleMode.TopIsland;
        }

        return CapsuleMode.BottomTaskbar;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleLayoutManagerTests" --no-restore`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs DynamicIslandBar/CapsuleConfigService.cs
git commit -m "feat: add capsule layout manager"
```

## Task 3: Add Theme Presets And Theme Manager

**Files:**
- Create: `DynamicIslandBar/CapsuleThemeManager.cs`
- Create: `DynamicIslandBar.Tests/CapsuleThemeManagerTests.cs`
- Modify: `DynamicIslandBar/CapsuleConfigService.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsuleThemeManagerTests
{
    [Theory]
    [InlineData(CapsuleThemePreset.ClassicDark)]
    [InlineData(CapsuleThemePreset.GlassGreen)]
    [InlineData(CapsuleThemePreset.SoftLight)]
    public void BuildTheme_ReturnsNamedPreset(CapsuleThemePreset preset)
    {
        var theme = CapsuleThemeManager.BuildTheme(preset);

        Assert.Equal(preset, theme.Preset);
        Assert.False(string.IsNullOrWhiteSpace(theme.CapsuleBackground));
        Assert.False(string.IsNullOrWhiteSpace(theme.PanelBackground));
    }

    [Fact]
    public void BuildTheme_PreservesBackgroundImageFields_WhenNotYetUsed()
    {
        var theme = CapsuleThemeManager.BuildTheme(
            CapsuleThemePreset.ClassicDark,
            backgroundImagePath: @"C:\wallpaper.png",
            backgroundImageOpacity: 0.65);

        Assert.Equal(@"C:\wallpaper.png", theme.BackgroundImagePath);
        Assert.Equal(0.65, theme.BackgroundImageOpacity);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleThemeManagerTests" --no-restore`

Expected: FAIL because `CapsuleThemeManager` and `CapsuleTheme` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace DynamicIslandBar;

public sealed record CapsuleTheme(
    CapsuleThemePreset Preset,
    string CapsuleBackground,
    string PanelBackground,
    string AccentColor,
    string BorderBrush,
    string? BackgroundImagePath,
    double BackgroundImageOpacity);

public static class CapsuleThemeManager
{
    public static CapsuleTheme BuildTheme(
        CapsuleThemePreset preset,
        string? backgroundImagePath = null,
        double backgroundImageOpacity = 0)
    {
        return preset switch
        {
            CapsuleThemePreset.GlassGreen => new CapsuleTheme(
                preset, "#CC142018", "#D91E2B24", "#4CD964", "#334CD964", backgroundImagePath, backgroundImageOpacity),
            CapsuleThemePreset.SoftLight => new CapsuleTheme(
                preset, "#E6F2F2F2", "#F5FFFFFF", "#1F8A70", "#221F8A70", backgroundImagePath, backgroundImageOpacity),
            _ => new CapsuleTheme(
                preset, "#EB141414", "#F01E1E1E", "#4CD964", "#22FFFFFF", backgroundImagePath, backgroundImageOpacity)
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleThemeManagerTests" --no-restore`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CapsuleThemeManager.cs DynamicIslandBar.Tests/CapsuleThemeManagerTests.cs DynamicIslandBar/CapsuleConfigService.cs
git commit -m "feat: add capsule theme presets"
```

## Task 4: Add Running Apps Composition Service

**Files:**
- Create: `DynamicIslandBar/RunningAppsService.cs`
- Create: `DynamicIslandBar.Tests/RunningAppsServiceTests.cs`
- Modify: `DynamicIslandBar/WindowManager.cs`
- Modify: `DynamicIslandBar/CapsuleConfigService.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class RunningAppsServiceTests
{
    [Fact]
    public void BuildSnapshot_GroupsWindowsByNormalizedAppId_AndMarksFavoriteAndHidden()
    {
        var config = new CapsuleConfig();
        CapsuleConfigMutator.SetFavorite(config, @"c:\apps\wechat.exe", true);
        config.HiddenApps.Add(@"c:\apps\qq.exe");

        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("微信", @"c:\apps\wechat.exe", 101, false),
                new WindowAppCandidate("微信聊天", @"c:\apps\wechat.exe", 102, false),
                new WindowAppCandidate("QQ", @"c:\apps\qq.exe", 201, false)
            ],
            config,
            visibleSlots: 2);

        Assert.Equal(2, snapshot.AllApps.Count);
        Assert.True(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\wechat.exe").IsFavorite);
        Assert.True(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\qq.exe").IsHiddenInCapsule);
    }

    [Fact]
    public void BuildSnapshot_UsesOverflowFolderWhenVisibleAppsExceedCapacity()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("A", @"a.exe", 1, false),
                new WindowAppCandidate("B", @"b.exe", 2, false),
                new WindowAppCandidate("C", @"c.exe", 3, false)
            ],
            new CapsuleConfig(),
            visibleSlots: 2);

        Assert.Single(snapshot.MainBarApps);
        Assert.Equal(2, snapshot.OverflowApps.Count);
        Assert.True(snapshot.HasOverflowFolder);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~RunningAppsServiceTests" --no-restore`

Expected: FAIL because `RunningAppsSnapshotBuilder`, `WindowAppCandidate`, and snapshot models do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
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
                group.Key,
                group.First().Title,
                config.KnownLaunchPaths.TryGetValue(group.Key, out var path) ? path : group.Key,
                true,
                config.FavoriteApps.Contains(group.Key),
                config.HiddenApps.Contains(group.Key),
                group.First().WindowHandle))
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var visibleApps = apps.Where(app => !app.IsHiddenInCapsule).ToList();
        var mainBarCapacity = visibleApps.Count > visibleSlots ? Math.Max(visibleSlots - 1, 0) : visibleSlots;
        var mainBarApps = visibleApps.Take(mainBarCapacity).ToList();
        var overflowApps = visibleApps.Skip(mainBarCapacity).ToList();

        return new RunningAppsSnapshot(
            apps,
            mainBarApps,
            overflowApps,
            overflowApps.Count > 0);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~RunningAppsServiceTests" --no-restore`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/RunningAppsService.cs DynamicIslandBar.Tests/RunningAppsServiceTests.cs DynamicIslandBar/WindowManager.cs DynamicIslandBar/CapsuleConfigService.cs
git commit -m "feat: add running apps snapshot builder"
```

## Task 5: Wire Layout, Theme, And Dynamic Apps Into MainWindow

**Files:**
- Modify: `DynamicIslandBar/MainWindow.xaml`
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
- Modify: `DynamicIslandBar/WindowManager.cs`
- Modify: `DynamicIslandBar/CapsuleConfigService.cs`
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`
- Modify: `DynamicIslandBar/CapsuleThemeManager.cs`
- Modify: `DynamicIslandBar/RunningAppsService.cs`

- [ ] **Step 1: Write the failing UI-facing tests for pure logic seams**

```csharp
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class MainWindowUiLogicTests
{
    [Fact]
    public void BuildAppsContextMenuState_ShowsOpenForStoppedFavoriteWithKnownPath()
    {
        var entry = new RunningAppEntry(
            "wechat",
            "WeChat",
            @"C:\Apps\WeChat.exe",
            false,
            true,
            false,
            0);

        var menuState = AppsMenuStateBuilder.Build(entry);

        Assert.True(menuState.CanOpenApp);
        Assert.False(menuState.CanCloseApp);
        Assert.True(menuState.CanToggleFavorite);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~MainWindowUiLogicTests" --no-restore`

Expected: FAIL because `AppsMenuStateBuilder` and `AppsMenuState` do not exist yet.

- [ ] **Step 3: Write minimal implementation and WPF integration**

```csharp
namespace DynamicIslandBar;

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
```

Then modify `MainWindow` to:
- replace the hard-coded demo dock with an `ItemsControl` or dynamic `StackPanel` host for running apps
- add a dedicated overflow-folder host and popup
- load config on startup
- compute layout metrics from `CapsuleLayoutManager`
- compute theme from `CapsuleThemeManager`
- refresh running apps on a timer using `RunningAppsService`
- populate the grid-button panel from the snapshot sections
- build app context menus from `AppsMenuStateBuilder`

- [ ] **Step 4: Run targeted tests and build verification**

Run:

```bash
dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~MainWindowUiLogicTests" --no-restore
dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --no-restore
dotnet build DynamicIslandBar/DynamicIslandBar.csproj --no-restore
```

Expected:
- targeted test PASS
- full test suite PASS
- build PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/MainWindow.xaml DynamicIslandBar/MainWindow.xaml.cs DynamicIslandBar/WindowManager.cs DynamicIslandBar/CapsuleConfigService.cs DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar/CapsuleThemeManager.cs DynamicIslandBar/RunningAppsService.cs DynamicIslandBar.Tests
git commit -m "feat: wire shell services into main window"
```

## Task 6: Add Drag-Snap Dual Mode And Final Integration Verification

**Files:**
- Modify: `DynamicIslandBar/MainWindow.xaml`
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`
- Modify: `DynamicIslandBar/CapsuleConfigService.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class DragSnapLogicTests
{
    [Fact]
    public void ResolveDropMode_ReturnsBottomWhenDroppedAwayFromTopThreshold()
    {
        var mode = CapsuleLayoutManager.ResolveDropMode(
            screenHeight: 1080,
            topAfterDrag: 820,
            currentMode: CapsuleMode.TopIsland);

        Assert.Equal(CapsuleMode.BottomTaskbar, mode);
    }

    [Fact]
    public void GetMetrics_UsesSmallerSystemDensityInTopMode()
    {
        var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);
        var bottom = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 1920, 1080);

        Assert.True(top.CapsuleHeight <= bottom.CapsuleHeight);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~DragSnapLogicTests" --no-restore`

Expected: FAIL if `ResolveDropMode` or metrics are incomplete for final snap behavior.

- [ ] **Step 3: Write minimal implementation**

```csharp
// MainWindow should add:
// - drag start tracking on capsule root
// - mouse move updates only while dragging
// - mouse up snaps through CapsuleLayoutManager.ResolveDropMode
// - config.Mode is updated and persisted
// - ApplyLayout(...) repositions the window and popup offsets
```

Key implementation outcomes:
- bottom mode snaps to bottom-center with wide width
- top mode snaps to top-center with compact width
- popup direction flips correctly
- current mode persists across relaunches

- [ ] **Step 4: Run full verification**

Run:

```bash
dotnet test DynamicIslandBar.Tests/DynamicIslandBar.Tests.csproj --no-restore
dotnet build DynamicIslandBar/DynamicIslandBar.csproj --no-restore
```

Manual verification checklist:
- close any running `DynamicIslandBar.exe`
- launch the app
- verify bottom mode renders a wide capsule
- drag to top and verify compact mode snaps correctly
- hover WiFi / volume / apps / overflow folder and verify popups stay on-screen
- click running-app icon to activate, click again to minimize
- right-click running-app icon and verify hidden/favorite/open/close state changes
- switch theme preset from capsule right-click menu

Expected:
- all tests PASS
- build PASS
- manual interactions match the spec

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/MainWindow.xaml DynamicIslandBar/MainWindow.xaml.cs DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar/CapsuleConfigService.cs
git commit -m "feat: add dual-mode drag snap shell"
```

## Self-Review

### Spec coverage

- Extensible shell structure: covered by Tasks 1-5
- Top/bottom dual mode: covered by Tasks 2 and 6
- Theme preset skeleton with future background support: covered by Task 3
- Running apps, overflow folder, management panel, and app actions: covered by Tasks 4 and 5
- Unified persistence for mode/theme/favorites/hidden apps/launch paths: covered by Task 1 and later wiring tasks

### Placeholder scan

- The only intentionally non-literal part is WPF shell markup in Task 5 Step 3 and Task 6 Step 3, which describes exact integration outcomes instead of dumping a brittle full XAML block into the plan.
- No `TODO`, `TBD`, or deferred placeholders remain.

### Type consistency

- `CapsuleMode` is shared across config, layout, and drag-snap tasks.
- `CapsuleThemePreset` is introduced in Task 1 and reused in Task 3.
- `RunningAppEntry` is introduced in Task 4 and reused by Task 5 menu-state logic.
- `LayoutMetrics` and `PopupFlowDirection` are introduced in Task 2 and reused by Task 6.
