# Floating Capsule Docking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为胶囊实现自由停留、四边吸附预览、以及左右边缘沿用顶部参数并整体旋转 90 度的统一吸附行为。

**Architecture:** 保留 `CapsuleMode` 作为持久化状态来源，在 `CapsuleLayoutManager` 中集中计算自由态/吸附态布局与预览轮廓；在 `MainWindow` 中只负责拖拽状态机、预览显示和落位应用。吸附预览单独建模为轮廓层，不直接复用真实胶囊本体，避免拖拽时布局抖动。

**Tech Stack:** .NET 8, WPF, xUnit

---

## File Structure

- Modify: `DynamicIslandBar/CapsuleConfigService.cs`
  - 新增 `Floating` 模式和自由停留位置、上次底部轮廓参数的持久化字段。
- Create: `DynamicIslandBar/CapsuleSnapPreview.cs`
  - 定义 `SnapEdge`、`CapsuleSnapPreview` 等轻量结构，避免把预览状态塞进 `MainWindow.xaml.cs`。
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`
  - 统一处理四边吸附判定、自由态窗口框体、预览轮廓框体、左右旋转角度。
- Modify: `DynamicIslandBar/MainWindow.xaml`
  - 新增吸附预览轮廓层。
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
  - 改造拖拽状态机，接入自由态、四边预览、左右旋转和配置恢复。
- Modify: `DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs`
  - 覆盖自由态字段的序列化/反序列化。
- Create: `DynamicIslandBar.Tests/CapsuleSnapPreviewTests.cs`
  - 覆盖预览轮廓的边缘参数规则。
- Modify: `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
  - 覆盖自由态、左右旋转和新的窗口框体规则。
- Modify: `DynamicIslandBar.Tests/DragSnapLogicTests.cs`
  - 覆盖“远离边缘回到自由态”的判定。

### Task 1: Layout Primitives And Config Persistence

**Files:**
- Create: `DynamicIslandBar/CapsuleSnapPreview.cs`
- Modify: `DynamicIslandBar/CapsuleConfigService.cs`
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`
- Modify: `DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs`
- Modify: `DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs`
- Modify: `DynamicIslandBar.Tests/DragSnapLogicTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Serialize_RoundTripsFloatingPlacement()
{
    var config = new CapsuleConfig
    {
        Mode = CapsuleMode.Floating,
        FloatingLeft = 312.5,
        FloatingTop = 228.25,
        LastBottomCapsuleWidth = 1280,
        LastBottomCapsuleHeight = 80
    };

    var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(config));

    Assert.Equal(CapsuleMode.Floating, restored.Mode);
    Assert.Equal(312.5, restored.FloatingLeft, precision: 2);
    Assert.Equal(228.25, restored.FloatingTop, precision: 2);
    Assert.Equal(1280, restored.LastBottomCapsuleWidth, precision: 2);
    Assert.Equal(80, restored.LastBottomCapsuleHeight, precision: 2);
}

[Fact]
public void ResolveDropMode_ReturnsFloating_WhenDroppedAwayFromAllEdges()
{
    var mode = CapsuleLayoutManager.ResolveDropMode(1920, 1080, 640, 420, CapsuleMode.TopIsland);
    Assert.Equal(CapsuleMode.Floating, mode);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleConfigServiceTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests"`

Expected: FAIL because `CapsuleMode.Floating` and the new persisted fields do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public enum CapsuleMode
{
    Floating,
    BottomTaskbar,
    TopIsland,
    LeftDock,
    RightDock
}

public sealed class CapsuleConfig
{
    public double FloatingLeft { get; set; }
    public double FloatingTop { get; set; }
    public double LastBottomCapsuleWidth { get; set; } = 0;
    public double LastBottomCapsuleHeight { get; set; } = 80;
}

public enum SnapEdge { None, Top, Bottom, Left, Right }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleConfigServiceTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests"`

Expected: PASS for the newly added config/layout assertions.

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CapsuleConfigService.cs DynamicIslandBar/CapsuleSnapPreview.cs DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs DynamicIslandBar.Tests/DragSnapLogicTests.cs
git commit -m "feat: add floating capsule layout primitives"
```

### Task 2: Preview Outline Model And Edge Rules

**Files:**
- Create: `DynamicIslandBar.Tests/CapsuleSnapPreviewTests.cs`
- Modify: `DynamicIslandBar/CapsuleSnapPreview.cs`
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void BuildPreview_UsesTopCapsuleMetrics_ForTopLeftAndRightEdges()
{
    var preview = CapsuleLayoutManager.BuildSnapPreview(
        SnapEdge.Left,
        screenWidth: 1920,
        screenHeight: 1080,
        topCapsuleWidth: 760,
        topCapsuleHeight: 72,
        bottomCapsuleWidth: 1500,
        bottomCapsuleHeight: 80);

    Assert.Equal(CapsuleMode.LeftDock, preview.Mode);
    Assert.Equal(760, preview.CapsuleWidth, precision: 1);
    Assert.Equal(72, preview.CapsuleHeight, precision: 1);
    Assert.Equal(90, preview.RotationDegrees, precision: 1);
}

[Fact]
public void BuildPreview_UsesLastBottomMetrics_ForBottomEdge()
{
    var preview = CapsuleLayoutManager.BuildSnapPreview(
        SnapEdge.Bottom,
        1920, 1080,
        topCapsuleWidth: 760,
        topCapsuleHeight: 72,
        bottomCapsuleWidth: 1320,
        bottomCapsuleHeight: 80);

    Assert.Equal(CapsuleMode.BottomTaskbar, preview.Mode);
    Assert.Equal(1320, preview.CapsuleWidth, precision: 1);
    Assert.Equal(80, preview.CapsuleHeight, precision: 1);
    Assert.Equal(0, preview.RotationDegrees, precision: 1);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~CapsuleSnapPreviewTests`

Expected: FAIL because `BuildSnapPreview` and `CapsuleSnapPreview` are not implemented yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public readonly record struct CapsuleSnapPreview(
    SnapEdge Edge,
    CapsuleMode Mode,
    double CapsuleWidth,
    double CapsuleHeight,
    double RotationDegrees,
    WindowFrame Frame);
```

在 `CapsuleLayoutManager.BuildSnapPreview(...)` 中实现：
- `Top/Left/Right` 使用顶部胶囊参数。
- `Left/Right` 在顶部参数基础上返回 `RotationDegrees = 90`。
- `Bottom` 使用 `LastBottomCapsuleWidth/Height`。

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~CapsuleSnapPreviewTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CapsuleSnapPreview.cs DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar.Tests/CapsuleSnapPreviewTests.cs
git commit -m "feat: add capsule snap preview model"
```

### Task 3: MainWindow Preview Layer And Drag State Skeleton

**Files:**
- Modify: `DynamicIslandBar/MainWindow.xaml`
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`
- Modify: `DynamicIslandBar.Tests/VisualLayerContractTests.cs`
- Create: `DynamicIslandBar.Tests/CapsuleSnapPreviewGeometryTests.cs` (only if a pure geometry helper is introduced to make preview positioning testable)

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void MainWindow_DeclaresSnapPreviewLayer()
{
    var xaml = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml");
    Assert.Contains("x:Name=\"CapsuleSnapPreviewLayer\"", xaml);
    Assert.Contains("x:Name=\"CapsuleSnapPreviewOutline\"", xaml);
}

[Fact]
public void MainWindow_CodeBehind_TracksFloatingAndPreviewState()
{
    var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
    Assert.Contains("_activeSnapPreview", code);
    Assert.Contains("UpdateSnapPreview(", code);
    Assert.Contains("ClearSnapPreview()", code);
    Assert.Contains("CaptureFloatingPosition()", code);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~VisualLayerContractTests|FullyQualifiedName~MainWindow"`

Expected: FAIL because the preview layer fields and helper methods are missing.

- [ ] **Step 3: Write the minimal implementation**

```xml
<Canvas x:Name="CapsuleSnapPreviewLayer"
        Width="{Binding ElementName=CapsuleGrid, Path=Width}"
        Height="{Binding ElementName=CapsuleGrid, Path=Height}"
        IsHitTestVisible="False"
        Panel.ZIndex="4">
    <Border x:Name="CapsuleSnapPreviewOutline"
            Visibility="Collapsed"
            BorderThickness="2"
            CornerRadius="40"/>
</Canvas>
```

```csharp
private CapsuleSnapPreview? _activeSnapPreview;
private double _floatingDragLeft;
private double _floatingDragTop;
```

然后在拖拽路径中新增：
- `UpdateSnapPreview(Point cursorScreenPoint)`
- `ApplySnapPreview(CapsuleSnapPreview preview)`
- `ClearSnapPreview()`
- `CaptureFloatingPosition()`
- 允许 `UpdateSnapPreview(...)` 在拖拽过程中做“最小可工作的预览刷新”：
  - 根据当前光标是否接近边缘决定显示或隐藏预览
  - 使用现有 `CapsuleLayoutManager.BuildSnapPreview(...)` 构造预览数据
  - 只更新预览层的显示，不做最终落位、不写配置、不切换最终模式
- 如果预览定位需要额外的纯几何计算 helper，可以在 `MainWindow.xaml.cs` 中新增一个小而纯的 helper，并用独立单测覆盖它

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter FullyQualifiedName~VisualLayerContractTests`

Expected: PASS for the new contract assertions.

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/MainWindow.xaml DynamicIslandBar/MainWindow.xaml.cs
git commit -m "feat: add snap preview layer and drag state"
```

### Task 4: Apply Floating Layout, Rotation, And Persistence

**Files:**
- Modify: `DynamicIslandBar/CapsuleLayoutManager.cs`
- Modify: `DynamicIslandBar/MainWindow.xaml.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void GetWindowFrame_UsesFloatingCoordinates_ForFloatingMode()
{
    var metrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.Floating, 1920, 1080);
    var frame = CapsuleLayoutManager.GetWindowFrame(CapsuleMode.Floating, metrics, 1920, 1080, floatingLeft: 360, floatingTop: 240);
    Assert.Equal(360, frame.Left, precision: 1);
    Assert.Equal(240, frame.Top, precision: 1);
}

[Fact]
public void ResolveDropMode_PrefersFloating_WhenLeavingLeftEdgeDuringDrag()
{
    var mode = CapsuleLayoutManager.ResolveDropMode(1920, 1080, leftAfterDrag: 220, topAfterDrag: 400, currentMode: CapsuleMode.LeftDock);
    Assert.Equal(CapsuleMode.Floating, mode);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests"`

Expected: FAIL because floating coordinates are not yet honored.

- [ ] **Step 3: Write the minimal implementation**

```csharp
var frame = CapsuleLayoutManager.GetWindowFrame(
    _capsuleConfig.Mode,
    _currentLayoutMetrics,
    screenWidth,
    screenHeight,
    _capsuleConfig.FloatingLeft,
    _capsuleConfig.FloatingTop);

if (_capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock)
{
    CapsuleBorder.RenderTransform = new RotateTransform(90);
}
else
{
    CapsuleBorder.RenderTransform = Transform.Identity;
}
```

同时在 `Capsule_DragMove` / `Capsule_DragEnd` 中：
- 进入边缘阈值时只刷新预览，不立即切换真实布局。
- 松手时根据当前预览边缘切换模式或保存 `FloatingLeft/Top`。
- 从 `LeftDock/RightDock` 拖离阈值后立刻清预览并回到 `Floating`。

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests|FullyQualifiedName~CapsuleSnapPreviewTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add DynamicIslandBar/CapsuleLayoutManager.cs DynamicIslandBar/MainWindow.xaml.cs DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs DynamicIslandBar.Tests/DragSnapLogicTests.cs
git commit -m "feat: apply floating docking behavior"
```

### Task 5: Final Verification

**Files:**
- Verify only

- [ ] **Step 1: Run targeted tests**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj --filter "FullyQualifiedName~CapsuleConfigServiceTests|FullyQualifiedName~CapsuleLayoutManagerTests|FullyQualifiedName~DragSnapLogicTests|FullyQualifiedName~CapsuleSnapPreviewTests|FullyQualifiedName~VisualLayerContractTests"`

Expected: PASS for all new docking-related coverage.

- [ ] **Step 2: Run build**

Run: `dotnet build DynamicIslandBar\\DynamicIslandBar.csproj`

Expected: `Build succeeded.`

- [ ] **Step 3: Run full test suite**

Run: `dotnet test DynamicIslandBar.Tests\\DynamicIslandBar.Tests.csproj`

Expected: 允许记录当前已知失败 `CenterCardPresentationPolicyTests.Resolve_IgnoresLiveSnapshotFromAnotherApp`，除此之外不新增失败。

- [ ] **Step 4: Manual verification checklist**

```text
1. 胶囊可停在屏幕中央或任意非边缘位置。
2. 靠近顶部/左侧/右侧时出现基于顶部参数的轮廓流光预览。
3. 靠近底部时出现基于上一次底部轮廓参数的预览。
4. 左右吸附落位后是完整胶囊旋转 90 度，不是简化侧栏。
5. 从左右边缘拖出时立即恢复自由态并继续跟手。
6. 重启应用后恢复最后一次自由位置或吸附状态。
```

- [ ] **Step 5: Commit verification-only changes if needed**

```bash
git status --short
```
