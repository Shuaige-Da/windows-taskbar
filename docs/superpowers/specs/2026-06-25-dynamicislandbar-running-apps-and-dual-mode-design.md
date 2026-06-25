# DynamicIslandBar Running Apps And Dual-Mode Design

> Status: Approved design spec for the next implementation planning step.
> Date: 2026-06-25
> Project: `DynamicIslandBar`

## 1. Goal

Add a release-grade running-apps experience to `DynamicIslandBar` so the capsule can behave like a lightweight custom Windows taskbar while still supporting a compact top "Dynamic Island" mode.

This design covers:
- real-time running app icons in the capsule
- app overflow handling
- app hide/show/favorite/open/close interactions
- app management panel behavior
- dual capsule modes: bottom taskbar mode and top dynamic-island mode

This design does not yet cover:
- full settings UI
- drag-and-drop app ordering
- multi-monitor support
- Windows 10 release support
- complex window-group previews

## 2. Product Intent

The capsule should no longer behave like a static demo bar with three hard-coded left-side icons.

Instead, it should become a live shell surface with two stable layouts:

1. `Bottom taskbar mode`
   A wide capsule that visually overlays the Windows taskbar area and behaves like a custom app dock/taskbar.

2. `Top dynamic-island mode`
   A compact capsule that snaps to the top of the screen and keeps the same core system controls and running-app capabilities in a smaller layout.

The same core app model, state, and interactions must work in both modes.

## 3. User-Facing Behavior

### 3.1 Running App Scope

The capsule only auto-discovers apps that have a visible top-level main window, close to how Windows taskbar presence feels.

Typical examples:
- WeChat
- QQ
- Douyin
- Codex
- browsers
- editors

Excluded by default:
- system shell windows
- hidden/tool windows
- this app itself: `DynamicIslandBar`
- background-only processes without a visible main window

### 3.2 Main Capsule App Icons

The left side of the capsule becomes a dynamic app-icon area.

Behavior:
- show running app icons in real time
- one icon per app, not one icon per window
- clicking an app icon toggles window state:
  - if the app window is minimized or not foreground, restore and activate it
  - if the representative window is already foreground, minimize it
- right-clicking an app icon opens a context menu

### 3.3 App Context Menu

Each app icon supports state-based actions.

Required actions:
- `隐藏图标` / `显示到胶囊`
- `关闭应用` / `打开应用`
- `添加到喜好` / `取消喜好`

Interpretation:
- `隐藏图标`: remove the icon from the capsule main bar only; the app keeps running
- `显示到胶囊`: put a hidden app back into the capsule main bar
- `关闭应用`: terminate the running app process
- `打开应用`: start the app again from a known executable path
- `添加到喜好`: pin the app as a favorite
- `取消喜好`: remove it from favorites

Action availability:
- `打开应用` is only enabled when a launch path is known
- `关闭应用` only appears for running apps
- `打开应用` appears for non-running favorite apps and for entries where a known launch path exists

### 3.4 Favorites

Favorites create a persistent pinned-app layer.

Behavior:
- a favorite app can remain visible even when not running
- a non-running favorite icon appears visually inactive
- clicking a non-running favorite launches the app
- if a favorite app is closed, it remains available as a pinned icon or in the management surfaces depending on its hidden state

Favorites are the foundation for a future "fixed apps" or "personal favorite apps" feature.

### 3.5 Hidden Apps

Hidden state only affects whether the app shows in the capsule main bar.

Behavior:
- hiding an app never stops the process
- hidden apps remain manageable from secondary surfaces
- a hidden favorite can still exist as a favorite, but it is not shown in the main capsule bar

### 3.6 Overflow Folder

The capsule main bar should not keep growing until it collides with time/system controls.

Instead:
- the capsule shows app icons only up to a layout-specific limit
- when the running visible app count exceeds that limit, the final slot in the main app area becomes an `overflow folder` icon
- hovering the overflow folder opens a hover panel above the capsule
- the overflow panel contains the apps that do not fit in the main bar
- apps inside the overflow panel support the same click and right-click behaviors as main-bar apps

Interaction model:
- hover folder icon -> show panel
- move pointer into panel -> keep panel interactive
- leave icon and panel -> auto-hide panel

### 3.7 Apps Management Panel

The current grid button remains, but its role changes.

It becomes the full management view for app state instead of only being a text list of visible windows.

Sections:
- `运行中应用`
- `已隐藏应用`
- `喜好应用`

The panel:
- appears above the capsule, like WiFi and volume
- does not overlap the capsule
- supports hover-to-open and hover-to-interact
- uses app icons plus labels instead of plain text rows only
- opens on hover from the grid button
- keeps the current grid-button click behavior of opening Task Manager

The overflow folder and the grid button have different jobs:
- `overflow folder`: quick access to extra visible running apps
- `grid button`: complete management surface

## 4. Dual Capsule Modes

### 4.1 Bottom Taskbar Mode

Bottom mode is the default mode on launch.

Behavior:
- capsule sits at the bottom of the primary screen
- capsule width automatically expands to match the visual rhythm of the desktop taskbar area
- capsule visually overlays the Windows taskbar area
- the Windows taskbar itself is not hidden as part of this feature
- the capsule acts as a wide custom taskbar

This mode prioritizes:
- running apps area
- overflow folder
- WiFi / volume / battery / time / grid button

### 4.2 Top Dynamic-Island Mode

If the user drags the capsule to the top snap area, it switches to top mode.

Behavior:
- capsule snaps to the top center of the primary screen
- width shrinks to a compact dynamic-island-like form
- all core system controls remain in the capsule:
  - WiFi
  - volume
  - battery
  - time
  - grid button
- running apps remain supported
- overflow logic remains supported
- popups should reposition downward or otherwise stay fully on-screen instead of rendering past the top edge

This mode changes layout density, not product capability.

### 4.3 Drag And Snap Rules

V1 supports only two final positions:
- top
- bottom

Behavior:
- user can drag the capsule
- if the drag ends near the top snap threshold, switch to top mode
- if the drag ends near the bottom snap threshold, switch to bottom mode
- free-floating middle positions are not preserved

This keeps the state machine simple and stable for V1.

## 5. System Architecture

The implementation should separate app discovery, app state, app launching/control, and UI layout.

### 5.1 New Service: `RunningAppsService`

Add a dedicated service responsible for discovering and composing running app entries.

Responsibilities:
- enumerate visible top-level windows
- normalize windows into app-level entries
- resolve process information
- resolve executable path when possible
- resolve icons
- provide a snapshot for UI refresh

This service should not directly own WPF UI elements.

### 5.2 Existing Service Boundary: `WindowManager`

`WindowManager` should remain a low-level Windows interop layer.

It may be expanded to provide:
- visible window enumeration with more metadata
- foreground-window checks
- restore/activate window
- minimize window

It should not become the place where app favorites/hidden state or WPF-specific menu behavior lives.

### 5.3 App State Layer

Add a light state layer for:
- hidden app ids
- favorite app ids
- known launch paths
- mode state: top or bottom

This state should be persisted and loaded at startup.

### 5.4 UI Composition

`MainWindow` should orchestrate:
- app icon rendering
- overflow folder rendering
- hover popup interaction
- dual-mode layout switching
- right-click context menus

Business logic should stay in services/helpers where possible so it can be tested.

## 6. Data Model

Add an app-level model similar to the following:

```text
AppEntry
- AppId
- DisplayName
- ExePath
- IconSource
- IsRunning
- IsFavorite
- IsHiddenInCapsule
- WindowHandles
- RepresentativeWindowHandle
```

Field meaning:
- `AppId`: stable identity, preferably based on normalized executable path; fallback to process name
- `DisplayName`: best available user-facing name
- `ExePath`: used for reopen/start behavior
- `IconSource`: resolved app icon for WPF rendering
- `IsRunning`: whether a representative running process/window exists
- `IsFavorite`: persisted user preference
- `IsHiddenInCapsule`: persisted user preference
- `WindowHandles`: visible top-level windows belonging to this app
- `RepresentativeWindowHandle`: the chosen window for activate/minimize behavior

## 7. Discovery And Refresh Rules

### 7.1 Discovery

Discovery rules:
- only visible top-level windows
- must have non-empty title
- exclude shell/system windows
- exclude this app
- group windows by app identity

V1 multi-window rule:
- if an app has multiple windows, it still becomes one `AppEntry`
- one representative window is chosen for toggle behavior
- V1 does not provide per-window preview or per-window list switching

### 7.2 Refresh Strategy

Use a low-frequency polling refresh first.

Target:
- approximately every `800ms` to `1200ms`

Reason:
- simple
- stable
- enough for V1 responsiveness
- avoids early complexity of a full WinEventHook-driven system

Future optimization can replace or augment polling with event-driven updates.

### 7.3 Failure Tolerance

Any individual failure to read:
- process info
- path
- icon
- window handle

must fail soft for that app entry, not for the whole UI refresh.

## 8. Persistence

Use a lightweight JSON config file for this feature set.

Required persisted fields:
- `FavoriteApps`
- `HiddenAppIds`
- `KnownLaunchPaths`
- `CapsuleMode`

Optional later extension:
- `DisplayOrder`

Rules:
- known launch path should be updated when the app is discovered with a valid path
- hidden state and favorite state are user-controlled, not derived
- mode state should be restored on next launch

V1 does not include a dedicated settings UI for editing these values manually.

## 9. UI Layout Details

### 9.1 Replace Demo Dock

The current hard-coded left-side app cards should be replaced with a dynamic app icon container.

V1 visual rules:
- consistent rounded icon buttons
- icons sized for both top and bottom mode
- inactive favorite icons use reduced opacity or grayscale-like styling
- hidden apps do not render in the main capsule bar

### 9.2 Overflow Folder Panel

Add a popup state for the overflow folder that mirrors existing WiFi/volume/apps hover behavior.

Required behavior:
- hover icon opens panel
- panel remains interactive on hover
- auto-close when pointer leaves icon and panel
- panel position must not overlap the capsule
- in top mode, the panel must render below or around the capsule so the popup stays inside the screen bounds

### 9.3 Apps Management Panel Layout

The grid-button popup should be upgraded from a simple text list to an app-management panel with grouped sections.

Each row or tile should show:
- icon
- display name
- status cues when useful

The panel must support:
- app click action
- app right-click action
- hidden/favorite/running distinctions
- showing hidden apps with a `显示到胶囊` recovery path

### 9.4 Mode-Specific Layout

Bottom mode:
- longer width
- higher visible icon capacity
- taskbar-like spacing

Top mode:
- shorter width
- lower visible icon capacity
- tighter spacing

Both modes:
- keep WiFi / volume / battery / time / grid button visible
- keep app overflow handling active

## 10. Interaction Semantics

### 10.1 Single Click

For a running app:
- if representative window is not foreground or is minimized: restore and activate
- if representative window is already foreground: minimize

For a non-running favorite:
- if launch path exists: launch app

### 10.2 Right Click

Right-click should open a WPF context menu for the selected app entry.

Menu labels are stateful:
- hidden vs visible
- running vs not running
- favorite vs not favorite

### 10.3 Close / Open Semantics

If a non-favorite running app is closed:
- it disappears from the main bar when the next refresh sees it stopped

If a favorite app is closed:
- it stays as a non-running favorite entry
- user can reopen it later

## 11. Edge Cases

### 11.1 Missing Executable Path

If executable path cannot be resolved:
- the app may still be shown while running
- activate/minimize still works if a valid window exists
- `打开应用` should be disabled
- `添加到喜好` may be disabled if reopening cannot be supported reliably

### 11.2 Missing Icon

Fallback order:
1. app-specific icon from path
2. process/default executable icon
3. generic placeholder icon

### 11.3 Self-Filtering

`DynamicIslandBar` must not appear in:
- main running apps bar
- overflow folder
- app management panel

### 11.4 Too Many Apps

If there are more running apps than the main bar can show:
- show visible capacity minus one
- reserve the final slot for the overflow folder

### 11.5 Favorite + Hidden

If an app is both favorite and hidden:
- it does not appear in the main capsule bar
- it remains in the management surfaces
- it can still be reopened if not running and a launch path exists

## 12. Error Handling

The feature must not make the existing capsule unstable.

Requirements:
- app discovery must not block the UI thread
- popup refresh must remain responsive
- icon extraction failures must not crash popup rendering
- process termination failures must fail gracefully
- app launch failures must fail gracefully

Prefer silent fallback plus later diagnostics logging instead of intrusive modal errors in V1.

## 13. Testing Strategy

### 13.1 Logic Tests

Add tests for:
- grouping windows into app entries
- merging persisted hidden/favorite state into live entries
- overflow partition logic
- favorite close/reopen behavior
- label/state mapping for right-click actions

### 13.2 UI/Integration Verification

Verify manually or through integration-friendly seams:
- bottom mode layout renders correctly
- top mode layout renders correctly
- drag to top snaps correctly
- drag to bottom snaps correctly
- hover behavior works for overflow panel
- grid-button panel still works
- WiFi/volume/battery interactions are not regressed
- single-click app toggle behavior works

## 14. Out Of Scope For This Implementation Cycle

Not included in this design cycle:
- drag-and-drop reordering
- multi-window grouped previews
- multi-monitor support
- hiding the system taskbar automatically
- advanced taskbar mirroring from Windows shell internals
- formal settings UI for this feature
- rich animations between all intermediate drag states

## 15. Recommended Implementation Direction

Implementation planning should follow this structure:

1. add low-level window/app discovery support
2. add app state persistence
3. add dynamic app icon rendering
4. add overflow folder behavior
5. add upgraded grid-button app management panel
6. add dual top/bottom mode layout and drag-snap behavior
7. add tests and regression verification

This keeps the work aligned with the current codebase while avoiding a large unstable rewrite.
