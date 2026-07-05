# 胶囊任务栏功能参数与实现档案

最后更新：`2026-06-30`

## 目的

这份档案是当前胶囊任务栏的总备份。

它记录三类内容：

1. 当前有哪些持久化配置项
2. 这些配置在代码里最终通过什么算法和规则生效
3. 以后改代码时，哪些地方最容易引发回归，以及该优先看哪些测试

以后如果某次修改把别的功能带坏了，先查这份档案，再查对应 bug 日志。

## 配置与状态文件

### 胶囊配置

- 文件路径：`%LOCALAPPDATA%\\DynamicIslandBar\\capsule-config.json`
- 定义位置：[CapsuleConfigService.cs](D:/UI-win/DynamicIslandBar/CapsuleConfigService.cs)

### 权限状态

- 文件路径：`%LOCALAPPDATA%\\DynamicIslandBar\\permissions.json`
- 定义位置：[PermissionService.cs](D:/UI-win/DynamicIslandBar/PermissionService.cs)

## 当前持久化配置项

### 胶囊主配置

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `Mode` | `BottomTaskbar` | 胶囊模式：底部任务栏 / 顶部灵动岛 |
| `ThemePreset` | `ClassicDark` | 主题预设 |
| `FavoriteApps` | 空 | 固定在胶囊里的喜爱应用 |
| `HiddenApps` | 空 | 从胶囊主栏隐藏的应用 |
| `KnownLaunchPaths` | 空 | 记录应用启动路径，用于重新打开应用 |
| `BackgroundImagePath` | `null` | 背景图片路径 |
| `BackgroundImageOpacity` | `0` | 背景图片透明度 |
| `BackgroundImageStretchMode` | `null` | 背景图片拉伸模式 |
| `GlassOpacityPercent` | `72` | 胶囊和浮层的玻璃透明度 |
| `ShadowPercent` | `0` | 胶囊和浮层阴影强度 |
| `GlowIntensityPercent` | `82` | 流光亮度 |
| `GlowThicknessPercent` | `42` | 流光粗细 |
| `GlowSpeedPercent` | `58` | 流光速度 |
| `CapsuleThicknessPercent` | `100` | 胶囊粗细 |
| `CapsuleLengthPercent` | `100` | 胶囊长度 |
| `TopDockCapsuleLengthPercent` | `0` | 顶部 / 左右吸附胶囊长度；`0%` 保持顶部默认长度 |
| `CenterCardWidthPercent` | `58` | 中心卡片宽度比例档位 |
| `CenterCardAppId` | `null` | 当前锁定到中心卡片的应用 |

### 权限项

| 权限 | 用途 |
| --- | --- |
| `WifiNearbyNetworks` | 读取 WiFi 列表 |
| `WifiControl` | 连接 / 断开 WiFi |
| `AudioControl` | 音量和播放控制 |
| `RunningApps` | 读取运行中应用 |

当前初始化行为：

- `PermissionService.Initialize(defaultAllowAll: true)`：本机默认全部接受
- `AllowAll = true` 时，不再逐项弹权限框

## 当前实现规则与算法

这些规则不是简单存进配置文件的值，而是配置最终在代码里被转换成的视觉和行为。

### 1. 胶囊布局基线

定义位置：[CapsuleLayoutManager.cs](D:/UI-win/DynamicIslandBar/CapsuleLayoutManager.cs)

| 模式 | 基础宽度 | 基础高度 | 弹层方向 | 基础应用槽位 | 系统任务栏 |
| --- | --- | --- | --- | --- | --- |
| `BottomTaskbar` | `screenWidth` | `80` | `Up` | `8` | 隐藏 |
| `TopIsland` | `760` | `72` | `Down` | `3` | 显示 |

固定规则：

- 拖动后 `top <= 72` 会吸附成顶部灵动岛模式
- 顶部模式窗口 `Top = 0`
- 底部模式窗口压到底部任务栏区域

### 2. 胶囊粗细映射

定义位置：[CapsuleAppearanceMapper.cs](D:/UI-win/DynamicIslandBar/CapsuleAppearanceMapper.cs)

- `CapsuleThicknessPercent = 0%` 时，实际高度约等于基础高度的 `66.7%`
- `CapsuleThicknessPercent = 100%` 时，实际高度等于基础高度的 `100%`
- 当前公式保留一个最小安全高度，避免应用图标和中心卡片内容被压扁

调节入口：

- 外观设置菜单里的“胶囊粗细”
- 胶囊外轮廓边缘的隐形拖拽区域，水平模式拖上下边，左右吸附模式拖左右边

### 3. 胶囊长度映射

定义位置：[CapsuleAppearanceMapper.cs](D:/UI-win/DynamicIslandBar/CapsuleAppearanceMapper.cs)

底部模式：

- `0%` 长度 = `760px`，如果屏幕更窄则取屏幕宽
- `100%` 长度 = 当前主屏宽度

顶部模式：

- 使用独立的 `TopDockCapsuleLengthPercent`
- `0%` 长度 = `760px` 顶部默认胶囊长度
- `100%` 长度 = 当前主屏宽度

左右吸附模式：

- 与顶部模式共用 `TopDockCapsuleLengthPercent`
- `0%` 长度 = `760px` 顶部默认胶囊长度
- `100%` 长度 = 当前主屏高度减去上下安全边距
- 视觉上仍按顶部胶囊参数生成，再转为竖向主轴布局

调节入口：

- 外观设置菜单里的“胶囊长度”
- 胶囊两端边缘的弧形拖拽手柄，水平模式拖左右端，左右吸附模式拖上下端

### 4. 流光映射

定义位置：[CapsuleAppearanceMapper.cs](D:/UI-win/DynamicIslandBar/CapsuleAppearanceMapper.cs)

- `GlowThicknessPercent` 映射到边框厚度：`0.8` 到 `3.6`
- `GlowSpeedPercent` 映射到动画时长：`4.2s` 到 `1.2s`
- `GlowIntensityPercent` 决定外层流光和浮层边框亮度
- 悬浮应用图标时，流光会改成对应图标主色

### 5. 中心卡片宽度映射

定义位置：[CenterCardLayoutPolicy.cs](D:/UI-win/DynamicIslandBar/CenterCardLayoutPolicy.cs)

- `CenterCardWidthPercent` 当前表示“中心卡片占胶囊宽度的比例档位”
- 比例范围：`18%` 到 `40%`
- 实际宽度不是只看比例，还要受中间可用槽位约束

当前最终公式：

`min(胶囊比例宽度, 中间槽位宽度 - 36px 外边距 - 16px 安全间距)`

附带规则：

- `< 320px`：`Minimal`
- `320px - 429px`：`Compact`
- `>= 430px`：`Full`

这会决定中心卡片里播放器按钮显示多少个。

### 6. 中心卡片展示策略

定义位置：[CenterCardPresentationPolicy.cs](D:/UI-win/DynamicIslandBar/CenterCardPresentationPolicy.cs)

音乐应用：

- 未悬浮且正在播放：显示歌词跑马灯
- 已悬浮或已暂停：显示详细信息和播放器控件
- 歌词层会按中心卡片宽度自适应：宽卡片显示左右音符，较窄卡片优先保留歌词文本空间
- 歌词文本能放下时直接显示；只有文本超出可视宽度时才启动向左滚动

非音乐应用：

- 始终显示应用详情
- 当前副文本格式：`状态 · 单击激活 / 再次单击最小化`

### 7. 主栏应用槽位规则

定义位置：[MainWindow.xaml.cs](D:/UI-win/DynamicIslandBar/MainWindow.xaml.cs)

- 顶部模式：实际主栏应用槽位动态范围 `2 - 3`
- 底部模式：实际主栏应用槽位动态范围 `3 - 8`
- 超出的应用进入收纳夹和管理面板

### 8. 刷新频率

定义位置：[RunningAppsRefreshPolicy.cs](D:/UI-win/DynamicIslandBar/RunningAppsRefreshPolicy.cs)

- 交互中刷新：`1s`
- 空闲刷新：`3s`
- 中心卡片媒体刷新定时器：`2s`

### 9. 看门狗

定义位置：[TaskbarRestoreWatchdog.cs](D:/UI-win/DynamicIslandBar/TaskbarRestoreWatchdog.cs)

- 主程序会启动一个轻量看门狗进程
- 用途：主进程异常退出后恢复系统任务栏
- 这也是测试和构建时经常出现 `DynamicIslandBar.exe` 被占用的来源之一

## 高风险回归区

以后改这些地方时，默认按高风险处理：

1. `CapsuleAppearanceMapper`：影响粗细、长度、流光、玻璃感
2. `CapsuleLayoutManager`：影响顶部 / 底部模式切换、位置和任务栏覆盖
3. `CenterCardLayoutPolicy`：影响中心卡片裁切、自适应、按钮密度
4. `CenterCardPresentationPolicy`：影响歌词 / 播放器 / 应用详情切换
5. `MainWindow.xaml` 和 `MainWindow.xaml.cs`：影响浮层、右键菜单、主布局和交互
6. `PermissionService`：影响 WiFi / 音频 / 运行应用授权行为

## 对应测试入口

如果上面这些规则变了，优先检查这些测试是否也需要同步调整：

| 场景 | 测试文件 |
| --- | --- |
| 配置序列化与默认值 | [CapsuleConfigServiceTests.cs](D:/UI-win/DynamicIslandBar.Tests/CapsuleConfigServiceTests.cs) |
| 外观映射 | [CapsuleAppearanceSettingsTests.cs](D:/UI-win/DynamicIslandBar.Tests/CapsuleAppearanceSettingsTests.cs) |
| 顶部 / 底部布局 | [CapsuleLayoutManagerTests.cs](D:/UI-win/DynamicIslandBar.Tests/CapsuleLayoutManagerTests.cs) |
| 中心卡片宽度与密度 | [CenterCardLayoutPolicyTests.cs](D:/UI-win/DynamicIslandBar.Tests/CenterCardLayoutPolicyTests.cs) |
| 中心卡片展示策略 | [CenterCardPresentationPolicyTests.cs](D:/UI-win/DynamicIslandBar.Tests/CenterCardPresentationPolicyTests.cs) |
| 主窗口关键视觉契约 | [VisualLayerContractTests.cs](D:/UI-win/DynamicIslandBar.Tests/VisualLayerContractTests.cs) |
| 主窗口 UI 行为 | [MainWindowUiLogicTests.cs](D:/UI-win/DynamicIslandBar.Tests/MainWindowUiLogicTests.cs) |
| 看门狗与任务栏恢复 | [TaskbarRestoreWatchdogTests.cs](D:/UI-win/DynamicIslandBar.Tests/TaskbarRestoreWatchdogTests.cs) |

## 以后修改代码时的约定

1. 改前先看这份功能档案
2. 再看 [bug 日志索引](D:/UI-win/docs/bug-log-index.md)，找到相关历史问题
3. 如果改了参数语义、默认值、映射规则，必须同步更新这份档案
4. 如果修了 bug，必须在 `docs/bug-logs/` 下新增或追加对应日志
5. 改完代码后至少跑相关测试；如果碰到 UI、布局、悬浮层、中心卡片，还要做一次实际视觉验证
