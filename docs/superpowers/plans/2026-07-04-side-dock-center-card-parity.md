# Side Dock Center Card Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让顶部吸附与底部胶囊保持同构，让左右吸附在保留底部全部功能的前提下改为竖向阅读结构，并修复左右态中心卡片歌词常态、详细态和长度调节的一致性。

**Architecture:** 继续保留现有 `CenterCardLyricsLayer + CenterCardDetailsLayer` 的双层渲染结构，但把展示语义统一回 `CenterCardPresentationPolicy`，不再允许侧边模式单独解释“主文案/副文案”的含义。布局差异只通过 `CenterCardLayoutPolicy`、`CapsuleLayoutManager` 和 `MainWindow` 的方向切换来表达：顶部仍走横向，左右走竖向，歌词动画方向与详细页展开方向随主轴切换。

**Tech Stack:** .NET 8, WPF, xUnit

---

## File Structure

- Modify: `DynamicIslandBar/CenterCardPresentationPolicy.cs`
  - 移除侧边模式专用的 `preferLyricsInDetails` 语义分叉，统一音乐常态 / 悬浮详细态文案规则。
- Modify: `DynamicIslandBar/CenterCardLayoutPolicy.cs`
  - 统一中心卡片主轴长度映射和歌词层布局参数，确保顶部/底部与左右模式共用同一比例规则。
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`
  - 保持顶部/左右模式默认参数与外侧弹出方向契约，补齐实现注释和可测入口。
- Modify: `DynamicIslandBar/MainWindow.xaml`
  - 保持一套中心卡片视图层，确保歌词层和详细层都支持横向 / 竖向重排，不新增第二套侧边 UI。
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
  - 统一中心卡片状态绑定、歌词动画方向、详细态重排、长度拖拽与浮层展开方向。
- Modify: `DynamicIslandBar.Tests/CenterCardPresentationPolicyTests.cs`
  - 锁定顶部/底部/侧边共享的中心卡片语义。
- Modify: `DynamicIslandBar.Tests/CenterCardLayoutPolicyTests.cs`
  - 锁定中心卡片比例映射与左右歌词布局契约。
- Modify: `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
  - 锁定顶部参数继承与左右外侧弹出方向契约。
- Modify: `DynamicIslandBar.Tests/MainWindowUiLogicTests.cs`
  - 锁定纯逻辑辅助方法和外侧浮层几何规则。
- Modify: `DynamicIslandBar.Tests/VisualLayerContractTests.cs`
  - 锁定 `MainWindow` 中的歌词层、详细层、竖向动画与长度拖拽契约。

### Task 1: Lock Unified Center Card Semantics In Policy Tests

**Files:**
- Modify: `DynamicIslandBar.Tests/CenterCardPresentationPolicyTests.cs`
- Modify: `DynamicIslandBar/CenterCardPresentationPolicy.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Build_UsesSongAndArtistAsPrimaryTextForHoveredMusicDetails()
{
    var app = CreateApp("cloudmusic", "网易云音乐");
    var media = new CenterCardMediaSnapshot(
        IsMusicApp: true,
        IsPlaying: true,
        Title: "Life's A Struggle",
        Artist: "宋岳庭",
        Lyric: "妈妈给我生命 现在让我自生自灭");

    var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: true);

    Assert.Equal(CenterCardDisplayMode.MusicDetails, state.Mode);
    Assert.Equal("Life's A Struggle - 宋岳庭", state.PrimaryText);
    Assert.Equal("歌词：妈妈给我生命 现在让我自生自灭", state.SecondaryText);
    Assert.True(state.ShowTransportControls);
}

[Fact]
public void Build_UsesLyricsMarqueeWhenLyricExistsEvenIfPlaybackFlagLags()
{
    var app = CreateApp("cloudmusic", "网易云音乐");
    var media = new CenterCardMediaSnapshot(
        IsMusicApp: true,
        IsPlaying: false,
        Title: "Life's A Struggle",
        Artist: "宋岳庭",
        Lyric: "妈妈给我生命 现在让我自生自灭");

    var state = CenterCardPresentationPolicy.Build(app, "当前窗口", media, isHovered: false);

    Assert.Equal(CenterCardDisplayMode.MusicLyricsMarquee, state.Mode);
    Assert.Equal("妈妈给我生命 现在让我自生自灭", state.PrimaryText);
    Assert.True(state.ShowLyricsMarquee);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~CenterCardPresentationPolicyTests`

Expected: FAIL because the current implementation still uses the side-dock-only `preferLyricsInDetails` path and can place lyric text in the detailed view primary slot.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public static CenterCardPresentation Build(
    RunningAppEntry? app,
    string status,
    CenterCardMediaSnapshot? media,
    bool isHovered)
{
    if (app == null)
    {
        return new CenterCardPresentation(
            CenterCardDisplayMode.Hidden,
            string.Empty,
            string.Empty,
            ShowLyricsMarquee: false,
            ShowTransportControls: false,
            ShowAppActions: false);
    }

    if (media is { IsMusicApp: true })
    {
        var hasLyric = !string.IsNullOrWhiteSpace(media.Lyric);
        var titleArtist = string.IsNullOrWhiteSpace(media.Artist)
            ? media.Title
            : $"{media.Title} - {media.Artist}";

        if (!isHovered && (media.IsPlaying || hasLyric))
        {
            return new CenterCardPresentation(
                CenterCardDisplayMode.MusicLyricsMarquee,
                hasLyric ? media.Lyric : titleArtist,
                string.Empty,
                ShowLyricsMarquee: true,
                ShowTransportControls: false,
                ShowAppActions: false);
        }

        return new CenterCardPresentation(
            CenterCardDisplayMode.MusicDetails,
            titleArtist,
            hasLyric ? $"歌词：{media.Lyric}" : (media.IsPlaying ? "正在播放" : "已暂停"),
            ShowLyricsMarquee: false,
            ShowTransportControls: true,
            ShowAppActions: false);
    }

    return new CenterCardPresentation(
        CenterCardDisplayMode.AppDetails,
        app.DisplayName,
        $"{status} · 单击激活 / 再次单击最小化",
        ShowLyricsMarquee: false,
        ShowTransportControls: false,
        ShowAppActions: true);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~CenterCardPresentationPolicyTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CenterCardPresentationPolicy.cs DynamicIslandBar.Tests/CenterCardPresentationPolicyTests.cs
git commit -m "fix: unify center card presentation semantics"
```

### Task 2: Lock Main-Axis Length And Lyrics Layout Parity

**Files:**
- Modify: `DynamicIslandBar.Tests/CenterCardLayoutPolicyTests.cs`
- Modify: `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
- Modify: `DynamicIslandBar/CenterCardLayoutPolicy.cs`
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(CapsuleMode.TopIsland, 760, 58, 233.8)]
[InlineData(CapsuleMode.LeftDock, 760, 58, 233.8)]
[InlineData(CapsuleMode.RightDock, 760, 58, 233.8)]
public void MapWidth_ScalesWithTopRatioForTopAndSideModes(
    CapsuleMode mode,
    double capsuleWidth,
    int percent,
    double expected)
{
    Assert.Equal(expected, CenterCardLayoutPolicy.MapWidth(mode, capsuleWidth, percent), precision: 1);
}

[Theory]
[InlineData(198, false, false)]
[InlineData(360, true, false)]
[InlineData(520, true, true)]
public void GetLyricsLayout_PrioritizesTextSpaceAcrossHorizontalAndVerticalModes(
    double centerCardExtent,
    bool expectedLeadingWave,
    bool expectedTrailingWave)
{
    var layout = CenterCardLayoutPolicy.GetLyricsLayout(centerCardExtent);

    Assert.Equal(expectedLeadingWave, layout.ShowLeftWave);
    Assert.Equal(expectedTrailingWave, layout.ShowRightWave);
}

[Fact]
public void GetMetrics_UsesTopPopupDefaultsForSideDockModes()
{
    var left = CapsuleLayoutManager.GetMetrics(CapsuleMode.LeftDock, 1920, 1080);
    var right = CapsuleLayoutManager.GetMetrics(CapsuleMode.RightDock, 1920, 1080);
    var top = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, 1920, 1080);

    Assert.Equal(top.CapsuleWidth, left.CapsuleWidth);
    Assert.Equal(top.CapsuleHeight, left.CapsuleHeight);
    Assert.Equal(PopupFlowDirection.Right, left.PopupDirection);
    Assert.Equal(PopupFlowDirection.Left, right.PopupDirection);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CenterCardLayoutPolicyTests|FullyQualifiedName~CapsuleLayoutManagerTests"`

Expected: FAIL if any remaining side-dock path still bypasses the shared percent mapping or diverges from top-mode geometry assumptions.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public static double MapWidth(CapsuleMode mode, double capsuleWidth, int percent)
{
    if (capsuleWidth <= 0)
    {
        return 0;
    }

    var ratio = MapPercentToRatio(percent);
    return capsuleWidth * ratio;
}

public static double MapSideDockExtent(double mappedTopLength, double availableHeight)
{
    if (availableHeight <= 0)
    {
        return 96d;
    }

    if (availableHeight <= 96d)
    {
        return availableHeight;
    }

    return Math.Clamp(mappedTopLength, 96d, availableHeight);
}
```

在 `CapsuleLayoutManager.GetMetrics(...)` 中继续保持：

```csharp
case CapsuleMode.LeftDock:
    return new CapsuleLayoutMetrics(
        topMetrics.CapsuleWidth,
        topMetrics.CapsuleHeight,
        topMetrics.VisibleAppSlots,
        PopupFlowDirection.Right);

case CapsuleMode.RightDock:
    return new CapsuleLayoutMetrics(
        topMetrics.CapsuleWidth,
        topMetrics.CapsuleHeight,
        topMetrics.VisibleAppSlots,
        PopupFlowDirection.Left);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CenterCardLayoutPolicyTests|FullyQualifiedName~CapsuleLayoutManagerTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CenterCardLayoutPolicy.cs DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar.Tests/CenterCardLayoutPolicyTests.cs DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs
git commit -m "test: lock center card extent parity rules"
```

### Task 3: Reflow The Shared Center Card View For Top And Side Modes

**Files:**
- Modify: `DynamicIslandBar/MainWindow.xaml`
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
- Modify: `DynamicIslandBar.Tests/VisualLayerContractTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void MainWindow_SideDockCenterCardUsesVerticalContentFlowWithoutSideSpecificPresentationBranch()
{
    var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
    var xaml = ReadMainWindowXaml();

    Assert.Contains("DockPanel.SetDock(CenterCardLyricsIcon, Dock.Top);", code);
    Assert.Contains("CenterCardLyricsIcon.VerticalAlignment = VerticalAlignment.Top;", code);
    Assert.Contains("Grid.SetRow(ActiveAppSummaryIcon, 0);", code);
    Assert.Contains("Grid.SetRow(CenterCardDetailsTextStack, 1);", code);
    Assert.Contains("Grid.SetRow(CenterCardTransportControls, 2);", code);
    Assert.DoesNotContain("preferLyricsInDetails: IsSideDockMode", code);
    Assert.Contains("x:Name=\"CenterCardLyricsLayer\"", xaml);
    Assert.Contains("x:Name=\"CenterCardDetailsLayer\"", xaml);
}

[Fact]
public void MainWindow_TopModeSharesBottomCenterCardPresentationPath()
{
    var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

    Assert.Contains("var state = CenterCardPresentationPolicy.Build(", code);
    Assert.DoesNotContain("preferLyricsInDetails", code);
    Assert.Contains("CenterCardLyricsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;", code);
    Assert.Contains("CenterCardDetailsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Collapsed : Visibility.Visible;", code);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~VisualLayerContractTests`

Expected: FAIL because `MainWindow.xaml.cs` still passes the side-specific `preferLyricsInDetails` flag.

- [ ] **Step 3: Write the minimal implementation**

```csharp
var state = CenterCardPresentationPolicy.Build(
    app,
    status,
    media,
    _isCenterCardHovered);

if (isSideDock)
{
    DockPanel.SetDock(CenterCardLyricsIcon, Dock.Top);
    CenterCardLyricsIcon.HorizontalAlignment = HorizontalAlignment.Center;
    CenterCardLyricsIcon.VerticalAlignment = VerticalAlignment.Top;

    Grid.SetRow(ActiveAppSummaryIcon, 0);
    Grid.SetRow(CenterCardDetailsTextStack, 1);
    Grid.SetRow(CenterCardTransportControls, 2);
    CenterCardTransportControls.Orientation = Orientation.Vertical;
}
else
{
    DockPanel.SetDock(CenterCardLyricsIcon, Dock.Left);
    CenterCardLyricsIcon.HorizontalAlignment = HorizontalAlignment.Left;
    CenterCardLyricsIcon.VerticalAlignment = VerticalAlignment.Center;
    CenterCardTransportControls.Orientation = Orientation.Horizontal;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~VisualLayerContractTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/MainWindow.xaml DynamicIslandBar/MainWindow.xaml.cs DynamicIslandBar.Tests/VisualLayerContractTests.cs
git commit -m "refactor: share center card layout contract across dock modes"
```

### Task 4: Fix Side-Dock Lyrics Motion, Detail Content, And Resize Interaction

**Files:**
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
- Modify: `DynamicIslandBar.Tests/MainWindowUiLogicTests.cs`
- Modify: `DynamicIslandBar.Tests/VisualLayerContractTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void MainWindow_SideDockLyricsUseBottomToTopDanmakuMotion()
{
    var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

    Assert.Contains("var usesVerticalLyricsFlow = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;", code);
    Assert.Contains("Canvas.SetTop(textBlock, verticalStartTop);", code);
    Assert.Contains("textBlock.BeginAnimation(Canvas.TopProperty, animation);", code);
    Assert.Contains("To = usesVerticalLyricsFlow ? -(textHeight + 28) : -(textWidth + 36)", code);
}

[Fact]
public void MainWindow_SideDockResizeUsesVerticalDeltaForCenterCardExtent()
{
    var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

    Assert.Contains("var delta = IsSideDockMode", code);
    Assert.Contains("e.VerticalChange * 2", code);
    Assert.Contains("SetCenterCardWidthPercent", code);
}

[Fact]
public void GetOverlayFrame_PlacesRightDockOverlayOnCapsuleOuterLeftSide()
{
    var frame = AppHoverOverlayLayoutPolicy.GetOverlayFrame(
        PopupFlowDirection.Left,
        iconLeft: 470,
        iconTop: 180,
        iconWidth: 40,
        iconHeight: 40,
        overlayWidth: 84,
        overlayHeight: 116,
        layerWidth: 540,
        layerHeight: 400);

    Assert.Equal(380, frame.Left);
    Assert.Equal(142, frame.Top);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~MainWindowUiLogicTests|FullyQualifiedName~VisualLayerContractTests"`

Expected: FAIL if any recent layout changes accidentally restore horizontal detail content, wrong lyric direction, or non-vertical resize behavior.

- [ ] **Step 3: Write the minimal implementation**

```csharp
var delta = IsSideDockMode
    ? e.VerticalChange * 2
    : e.HorizontalChange * 2;

var usesVerticalLyricsFlow = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;

if (usesVerticalLyricsFlow)
{
    Canvas.SetLeft(textBlock, 0);
    Canvas.SetTop(textBlock, verticalStartTop);
    textBlock.BeginAnimation(Canvas.TopProperty, animation);
}
else
{
    Canvas.SetLeft(textBlock, viewportWidth + (laneIndex * 28));
    Canvas.SetTop(textBlock, laneIndex * laneHeight);
    textBlock.BeginAnimation(Canvas.LeftProperty, animation);
}
```

并保持外侧弹出辅助路径：

```csharp
return mode switch
{
    CapsuleMode.LeftDock => PopupFlowDirection.Right,
    CapsuleMode.RightDock => PopupFlowDirection.Left,
    _ => _currentLayoutMetrics.PopupDirection
};
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~MainWindowUiLogicTests|FullyQualifiedName~VisualLayerContractTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/MainWindow.xaml.cs DynamicIslandBar.Tests/MainWindowUiLogicTests.cs DynamicIslandBar.Tests/VisualLayerContractTests.cs
git commit -m "fix: restore side dock lyric motion and resize parity"
```

### Task 5: Full Verification And Screenshot Review

**Files:**
- Modify: `DynamicIslandBar.Tests/CenterCardPresentationPolicyTests.cs`
- Modify: `DynamicIslandBar.Tests/CenterCardLayoutPolicyTests.cs`
- Modify: `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
- Modify: `DynamicIslandBar.Tests/MainWindowUiLogicTests.cs`
- Modify: `DynamicIslandBar.Tests/VisualLayerContractTests.cs`
- Modify: `DynamicIslandBar/MainWindow.xaml`
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`

- [ ] **Step 1: Run the focused automated test suite**

Run:

```bash
dotnet test DynamicIslandBar.Tests\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CenterCardPresentationPolicyTests|FullyQualifiedName~CenterCardLayoutPolicyTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~MainWindowUiLogicTests|FullyQualifiedName~VisualLayerContractTests" /p:UseSharedCompilation=false
```

Expected: PASS for all center-card and side-dock parity coverage.

- [ ] **Step 2: Run the project build**

Run:

```bash
dotnet build DynamicIslandBar\DynamicIslandBar.csproj /p:UseSharedCompilation=false
```

Expected: PASS with 0 build errors.

- [ ] **Step 3: Launch the app and verify left/right dock manually**

Run:

```bash
dotnet run --project DynamicIslandBar\DynamicIslandBar.csproj
```

Manual checks:

- 将胶囊拖到左侧吸附，确认图标保持正向。
- 常态中心卡片显示歌词，且歌词从下往上滑动。
- 悬浮中心卡片时显示歌曲名、歌手、播放器控件。
- 详细页出现在胶囊右侧。
- 拖动中心卡片长度，确认中心卡片主体同步变长。

- [ ] **Step 4: Repeat the visual pass for right dock**

Manual checks:

- 将胶囊拖到右侧吸附。
- 常态歌词仍在中心卡片内显示并自下而上滑动。
- 悬浮详细页出现在胶囊左侧。
- 播放器控件和歌曲信息完整，不回退成横排残留。

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/MainWindow.xaml DynamicIslandBar/MainWindow.xaml.cs DynamicIslandBar/CenterCardPresentationPolicy.cs DynamicIslandBar/CenterCardLayoutPolicy.cs DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar.Tests/CenterCardPresentationPolicyTests.cs DynamicIslandBar.Tests/CenterCardLayoutPolicyTests.cs DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs DynamicIslandBar.Tests/MainWindowUiLogicTests.cs DynamicIslandBar.Tests/VisualLayerContractTests.cs
git commit -m "fix: align top and side center card behavior with bottom mode"
```
