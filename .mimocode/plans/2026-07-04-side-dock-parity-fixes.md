# Side Dock Parity Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make left/right side-dock match bottom/top feature parity: remove the side-dock shadow regression, make detailed states render vertically, restore center-card length control, place hover/detail panels on the outer side of the capsule, and prevent the capsule from being dragged off-screen.

**Architecture:** Keep the existing single-capsule rendering model, but make side-dock behavior reuse the same configurable appearance, center-card percentage rules, and hover/detail features as the bottom capsule. Fixes stay localized to `CapsuleAppearanceMapper`, `CapsuleLayoutManager`, `CenterCardLayoutPolicy`, `AppHoverOverlayLayoutPolicy`, and `MainWindow` so drag constraints, popup placement, and side-dock content reflow remain testable without adding a second side-dock UI system.

**Tech Stack:** WPF (`Border`, `Grid`, `Thumb`, `DropShadowEffect`, animations), C#/.NET 8, xUnit contract/layout tests.

---

## File Map

- Modify: `D:\UI-win\DynamicIslandBar\CapsuleAppearanceMapper.cs`
  Responsibility: capsule shadow mapping and visual parity helpers.
- Modify: `D:\UI-win\DynamicIslandBar\CapsuleLayoutManager.cs`
  Responsibility: frame/bounds math, drag clamping helpers, side-dock geometry rules.
- Modify: `D:\UI-win\DynamicIslandBar\CenterCardLayoutPolicy.cs`
  Responsibility: center-card size mapping for horizontal and vertical layouts.
- Modify: `D:\UI-win\DynamicIslandBar\AppHoverOverlayLayoutPolicy.cs`
  Responsibility: side-dock hover/detail overlay placement and outer-edge routing.
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml`
  Responsibility: side-dock center-card resize handles, vertical detail layers, and hover overlay containers.
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml.cs`
  Responsibility: apply visual parity, enable side-dock center-card resizing, reflow detail/lyrics/popup states vertically, clamp drag position, and route side-dock hover/detail panels outward.
- Modify: `D:\UI-win\DynamicIslandBar.Tests\CenterCardLayoutPolicyTests.cs`
  Responsibility: verify side-dock extent mapping and width-percent parity.
- Modify: `D:\UI-win\DynamicIslandBar.Tests\CapsuleLayoutManagerTests.cs`
  Responsibility: verify drag-clamp, popup direction, and visible-bounds constraints.
- Modify: `D:\UI-win\DynamicIslandBar.Tests\MainWindowUiLogicTests.cs`
  Responsibility: verify app-hover overlay placement for left/right side-dock.
- Modify: `D:\UI-win\DynamicIslandBar.Tests\VisualLayerContractTests.cs`
  Responsibility: verify code-behind contracts for side-dock resize, vertical detail layout, outer-side popup routing, and shadow handling.

## Task 1: Remove Side-Dock Outer Black Shadow Regression

**Files:**
- Modify: `D:\UI-win\DynamicIslandBar\CapsuleAppearanceMapper.cs`
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\VisualLayerContractTests.cs`

- [ ] **Step 1: Write the failing contract test**

Add a contract asserting the main window uses a side-dock-aware shadow application path instead of blindly applying the same offset drop shadow in all modes.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~VisualLayerContractTests`
Expected: FAIL because the current code still applies `CapsuleAppearanceMapper.BuildShadowEffect(_capsuleConfig.ShadowPercent)` directly.

- [ ] **Step 3: Write minimal implementation**

Introduce a mode-aware shadow builder or application branch so side-dock keeps glass/glow but removes the heavy black offset shadow look that appears outside the tall vertical capsule.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~VisualLayerContractTests`
Expected: PASS.

## Task 2: Restore Side-Dock Center-Card Length Parity And Resize Control

**Files:**
- Modify: `D:\UI-win\DynamicIslandBar\CenterCardLayoutPolicy.cs`
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml`
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\CenterCardLayoutPolicyTests.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\VisualLayerContractTests.cs`

- [ ] **Step 1: Write the failing layout test**

Add tests that lock in two requirements:
1. side-dock center-card extent comes from the same user-configured percent rule instead of an arbitrary short clamp;
2. side-dock mode still supports changing that length interactively.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CenterCardLayoutPolicyTests|FullyQualifiedName~VisualLayerContractTests"`
Expected: FAIL because side-dock extent is still compressed separately and resize is disabled.

- [ ] **Step 3: Write minimal implementation**

Update side-dock center-card mapping so:
- top/left/right all honor the same stored `CenterCardWidthPercent`;
- side-dock converts that percent into vertical extent using the actual side capsule length and available vertical slot;
- side-dock resize handles become visibly active again instead of staying transparent, using vertical drag deltas;
- user appearance controls stay shared across bottom/top/left/right instead of introducing side-only overrides.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CenterCardLayoutPolicyTests|FullyQualifiedName~VisualLayerContractTests"`
Expected: PASS.

## Task 3: Reflow Side-Dock Detailed States Vertically And Route Them To The Outer Side

**Files:**
- Modify: `D:\UI-win\DynamicIslandBar\AppHoverOverlayLayoutPolicy.cs`
- Modify: `D:\UI-win\DynamicIslandBar\CapsuleLayoutManager.cs`
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml`
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\MainWindowUiLogicTests.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\CapsuleLayoutManagerTests.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\VisualLayerContractTests.cs`

- [ ] **Step 1: Write the failing layout and geometry tests**

Add tests that lock in three side-dock rules:
1. app hover overlays choose the outer side of the capsule (`Right` for left dock, `Left` for right dock);
2. side-dock popups use left/right placement instead of only top/bottom placement;
3. side-dock center-card detail and lyric states use a vertical content flow rather than leaving title/status/controls in a horizontal strip.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~MainWindowUiLogicTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~VisualLayerContractTests"`
Expected: FAIL because current hover overlays are always right-side horizontal pills and popup direction only supports up/down.

- [ ] **Step 3: Write minimal implementation**

Update side-dock detail behavior so:
- `PopupFlowDirection` can represent left/right outer-side placement and `ConfigurePopup(...)` maps that to WPF popup placement;
- left-docked capsule opens hover/detail popups on the right, right-docked capsule opens them on the left;
- app hover detail overlay uses a side-dock-aware frame policy and a vertical card layout;
- center-card detail mode keeps icon/text/controls aligned as a vertical stack, and lyric mode keeps the lyric flow entering from bottom to top.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~MainWindowUiLogicTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~VisualLayerContractTests"`
Expected: PASS.

## Task 4: Constrain Capsule Dragging So It Cannot Be Lost Off-Screen

**Files:**
- Modify: `D:\UI-win\DynamicIslandBar\CapsuleLayoutManager.cs`
- Modify: `D:\UI-win\DynamicIslandBar\MainWindow.xaml.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\CapsuleLayoutManagerTests.cs`
- Test: `D:\UI-win\DynamicIslandBar.Tests\DragSnapLogicTests.cs`

- [ ] **Step 1: Write the failing geometry test**

Add tests that verify floating and dragged capsule positions are clamped so the rendered capsule bounds remain recoverable on-screen.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests"`
Expected: FAIL because drag currently applies raw `Left`/`Top` deltas without clamping.

- [ ] **Step 3: Write minimal implementation**

Add a `CapsuleLayoutManager` helper that clamps desired window origin based on rendered capsule bounds and call it during drag and floating-position persistence. Preserve existing snap preview and drop-mode behavior.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests"`
Expected: PASS.

## Task 5: Run Side-Dock Visual Auto-Verification Before Closeout

**Files:**
- No production-file changes required unless visual verification finds a regression.

- [ ] **Step 1: Launch the desktop app in a debuggable build**

Run the local app from the current workspace so the latest side-dock changes are active.

- [ ] **Step 2: Exercise left and right docking manually through automation**

Before taking screenshots, drag the capsule into the left-dock and right-dock snap areas and let each dock settle into its attached state.

- [ ] **Step 3: Capture inspection screenshots**

Take screenshots for both left and right dock states and inspect:
- hover/detail overlays open on the capsule’s outer side;
- detailed content flows vertically;
- center-card icon sits in the top section rather than visually centered like the old horizontal layout;
- lyrics enter from the bottom and move upward when lyric mode is active;
- the capsule remains fully visible after dragging.

- [ ] **Step 4: If screenshots reveal issues, fix them before final verification**

Treat visual verification as a blocker, not an optional spot check.

## Final Verification

- [ ] Run: `dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CenterCardLayoutPolicyTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests|FullyQualifiedName~MainWindowUiLogicTests|FullyQualifiedName~VisualLayerContractTests"`
Expected: PASS with 0 failures.

- [ ] Run: `dotnet build DynamicIslandBar\DynamicIslandBar.csproj`
Expected: `已成功生成。`

- [ ] Manually verify in-app:
1. left/right dock no longer shows a heavy black outer shadow strip;
2. center-card can grow longer in side-dock and still reflects the stored width percent;
3. side-dock resize interaction changes vertical center-card length and the drag handles are actually usable;
4. app hover/detail overlays appear on the capsule outer side and use vertical content layout;
5. side-dock lyric state enters from bottom to top;
6. dragging cannot move the capsule so far that it disappears off-screen.
