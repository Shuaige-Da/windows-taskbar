using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Text;
using System.Globalization;
using Icon = System.Drawing.Icon;

namespace DynamicIslandBar
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _glowStopTimer;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _hoverCloseTimer;
        private readonly DispatcherTimer _capsuleHoverCollapseTimer;
        private readonly DispatcherTimer _appsRefreshTimer;
        private Storyboard? _glowSpinStoryboard;
        private Storyboard? _dockExpandStoryboard;
        private Storyboard? _dockCollapseStoryboard;
        private TranslateTransform? _capsuleGlowTransform;
        private bool _suppressVolumeEvent;
        private bool _windowLoaded;
        private int _wifiRefreshVersion;
        private int _volumeRefreshVersion;
        private int _runningAppsRefreshVersion;
        private PopupState? _pendingHoverClosePopup;
        private PermissionPromptState? _pendingPermissionPrompt;
        private WifiAccessIssue _lastWifiAccessIssue = WifiAccessIssue.None;
        private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> _accentCache = new(StringComparer.OrdinalIgnoreCase);
        private CapsuleConfig _capsuleConfig = new();
        private CapsuleTheme _currentTheme = CapsuleThemeManager.BuildTheme(CapsuleThemePreset.ClassicDark);
        private LayoutMetrics _currentLayoutMetrics;
        private RunningAppsSnapshot _runningAppsSnapshot = new([], [], [], false);
        private bool _isDraggingCapsule;
        private Point _dragStartPoint;
        private double _dragStartLeft;
        private double _dragStartTop;
        private string? _lastPrimaryActivatedAppId;
        private DateTime _lastPrimaryActivatedAtUtc;
        private RunningAppEntry? _hoveredApp;

        public MainWindow()
        {
            InitializeComponent();
            _glowStopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _glowStopTimer.Tick += GlowStopTimer_Tick;
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            _hoverCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            _hoverCloseTimer.Tick += HoverCloseTimer_Tick;
            _capsuleHoverCollapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
            _capsuleHoverCollapseTimer.Tick += CapsuleHoverCollapseTimer_Tick;
            _appsRefreshTimer = new DispatcherTimer { Interval = RunningAppsRefreshPolicy.GetInterval(false) };
            _appsRefreshTimer.Tick += AppsRefreshTimer_Tick;
        }

        private sealed class PopupState
        {
            public required Border Icon { get; init; }
            public required Border Panel { get; init; }
            public required Popup Popup { get; init; }
        }

        private sealed class PermissionPromptState
        {
            public required AppPermission Permission { get; init; }
            public required Action GrantedAction { get; init; }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PermissionService.Initialize(defaultAllowAll: true);
            _capsuleConfig = CapsuleConfigService.Load();
            _currentTheme = CapsuleThemeManager.BuildTheme(
                _capsuleConfig.ThemePreset,
                _capsuleConfig.BackgroundImagePath,
                _capsuleConfig.BackgroundImageOpacity);

            InitGlowAnimation();
            InitDockAnimations();
            HideDemoDockItems();
            ApplyLayout();
            ApplyTheme();
            UpdateClock();
            UpdateBatteryStatus();
            RefreshRunningAppsBar();
            _appsRefreshTimer.Start();
            _windowLoaded = true;
            Dispatcher.BeginInvoke(MaybeWriteLayoutDiagnostics, DispatcherPriority.Loaded);
        }

        private void HideDemoDockItems()
        {
            ItemMusic.Visibility = Visibility.Collapsed;
            ItemPhone.Visibility = Visibility.Collapsed;
            ItemNav.Visibility = Visibility.Collapsed;
        }

        private void MaybeWriteLayoutDiagnostics()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("DIB_LAYOUT_DIAGNOSTICS"), "1", StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var builder = new StringBuilder();
                var (screenWidth, screenHeight) = DisplayBoundsProvider.GetPrimaryScreenSize();
                builder.AppendLine($"screen={screenWidth}x{screenHeight}");
                builder.AppendLine($"window Left={Left} Top={Top} Width={Width} Height={Height}");
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DynamicIslandBar-layout.txt");
                File.WriteAllText(path, builder.ToString());

                AppendElementDiagnostics(builder, nameof(CapsuleGrid), CapsuleGrid);
                AppendElementDiagnostics(builder, nameof(CapsuleBorder), CapsuleBorder);
                AppendElementDiagnostics(builder, nameof(AppIconsHost), AppIconsHost);
                for (var index = 0; index < AppIconsHost.Children.Count; index++)
                {
                    if (AppIconsHost.Children[index] is FrameworkElement appIcon)
                    {
                        AppendElementDiagnostics(builder, $"AppIcon[{index}]", appIcon);
                    }
                }

                AppendElementDiagnostics(builder, nameof(ClockText), ClockText);
                AppendElementDiagnostics(builder, nameof(DateText), DateText);

                File.WriteAllText(path, builder.ToString());
            }
            catch
            {
                // Keep diagnostics best-effort only.
            }
        }

        private static void AppendElementDiagnostics(StringBuilder builder, string name, FrameworkElement element)
        {
            try
            {
                var relative = element.Parent is UIElement parent
                    ? element.TranslatePoint(new Point(0, 0), parent)
                    : new Point(double.NaN, double.NaN);
                var screen = element.PointToScreen(new Point(0, 0));
                builder.AppendLine(
                    $"{name} Actual={element.ActualWidth}x{element.ActualHeight} Parent=({relative.X},{relative.Y}) Screen=({screen.X},{screen.Y}) Visibility={element.Visibility}");
            }
            catch (Exception ex)
            {
                builder.AppendLine($"{name} diagnostic failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ApplyLayout()
        {
            var (screenWidth, screenHeight) = DisplayBoundsProvider.GetPrimaryScreenSize();
            _currentLayoutMetrics = BuildCurrentLayoutMetrics(screenWidth, screenHeight);
            var frame = CapsuleLayoutManager.GetWindowFrame(
                _capsuleConfig.Mode,
                _currentLayoutMetrics,
                screenWidth,
                screenHeight);

            Width = frame.Width;
            Height = frame.Height;
            Left = frame.Left;
            Top = frame.Top;

            CapsuleGrid.Width = Width - 20;
            CapsuleBorder.Width = _currentLayoutMetrics.CapsuleWidth;
            CapsuleBorder.Height = _currentLayoutMetrics.CapsuleHeight;
            UpdateCapsuleCornerRadius(_currentLayoutMetrics.CapsuleHeight);
            CapsuleBorder.VerticalAlignment = _capsuleConfig.Mode == CapsuleMode.TopIsland
                ? VerticalAlignment.Top
                : VerticalAlignment.Center;
            CapsuleGrid.VerticalAlignment = _capsuleConfig.Mode == CapsuleMode.TopIsland
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;

            PermissionPromptPanel.VerticalAlignment = _capsuleConfig.Mode == CapsuleMode.TopIsland
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;
            PermissionPromptPanel.Margin = _capsuleConfig.Mode == CapsuleMode.TopIsland
                ? new Thickness(0, 118, 0, 0)
                : new Thickness(0, 0, 0, 118);

            ApplySystemTaskbarVisibility();
            ConfigurePopup(WifiPopup, _currentLayoutMetrics.PopupDirection, -144);
            ConfigurePopup(VolumePopup, _currentLayoutMetrics.PopupDirection, -134);
            ConfigurePopup(AppsPopup, _currentLayoutMetrics.PopupDirection, -142);
            ConfigurePopup(OverflowAppsPopup, _currentLayoutMetrics.PopupDirection, 0);
        }

        private LayoutMetrics BuildCurrentLayoutMetrics(double screenWidth, double screenHeight)
        {
            var metrics = CapsuleLayoutManager.GetMetrics(_capsuleConfig.Mode, screenWidth, screenHeight);
            var capsuleWidth = CapsuleAppearanceMapper.MapCapsuleWidth(
                _capsuleConfig.Mode,
                metrics.CapsuleWidth,
                _capsuleConfig.CapsuleLengthPercent);

            return metrics with
            {
                CapsuleWidth = capsuleWidth,
                VisibleAppSlots = MapVisibleAppSlots(_capsuleConfig.Mode, metrics.CapsuleWidth, capsuleWidth)
            };
        }

        private static int MapVisibleAppSlots(CapsuleMode mode, double baseWidth, double capsuleWidth)
        {
            var maxSlots = mode == CapsuleMode.TopIsland ? 5 : 8;
            var minSlots = mode == CapsuleMode.TopIsland ? 2 : 3;
            if (baseWidth <= 0)
            {
                return minSlots;
            }

            var ratio = Math.Clamp(capsuleWidth / baseWidth, 0, 1);
            return Math.Clamp((int)Math.Round(maxSlots * ratio), minSlots, maxSlots);
        }

        private void ApplySystemTaskbarVisibility()
        {
            if (_capsuleConfig.Mode == CapsuleMode.BottomTaskbar)
            {
                TaskbarManager.Hide();
                return;
            }

            TaskbarManager.Show();
        }

        private void UpdateCapsuleCornerRadius(double height)
        {
            CapsuleBorder.CornerRadius = new CornerRadius(height / 2);
        }

        private void ConfigurePopup(Popup popup, PopupFlowDirection direction, double horizontalOffset)
        {
            popup.Placement = direction == PopupFlowDirection.Up ? PlacementMode.Top : PlacementMode.Bottom;
            popup.HorizontalOffset = horizontalOffset;
            popup.VerticalOffset = direction == PopupFlowDirection.Up ? -12 : 12;
        }

        private void ApplyTheme()
        {
            _currentTheme = CapsuleThemeManager.BuildTheme(
                _capsuleConfig.ThemePreset,
                _capsuleConfig.BackgroundImagePath,
                _capsuleConfig.BackgroundImageOpacity);

            CapsuleBorder.Background = CapsuleAppearanceMapper.BuildBackgroundBrush(_capsuleConfig.GlassOpacityPercent);
            UpdateCapsuleGlowBrush(null);
            CapsuleBorder.BorderThickness = new Thickness(CapsuleAppearanceMapper.MapGlowThickness(_capsuleConfig.GlowThicknessPercent));
            CapsuleBorder.Effect = CapsuleAppearanceMapper.BuildShadowEffect(_capsuleConfig.ShadowPercent);
            ApplyCapsuleThickness();

            ApplyGlassPanelTheme(WifiPanel);
            ApplyGlassPanelTheme(VolumePanel);
            ApplyGlassPanelTheme(AppsPanel);
            ApplyGlassPanelTheme(OverflowAppsPanel);
            ApplyGlassPanelTheme(PermissionPromptPanel);
            ApplyGlassPanelTheme(AppHoverOverlayBackground);
        }

        private static Brush CreateBrush(string color)
        {
            return (Brush)new BrushConverter().ConvertFromString(color)!;
        }

        private void ApplyGlassPanelTheme(Border panel)
        {
            panel.Background = CapsuleAppearanceMapper.BuildPanelBackgroundBrush(_capsuleConfig.GlassOpacityPercent);
            panel.BorderBrush = CapsuleAppearanceMapper.BuildPanelBorderBrush(_capsuleConfig.GlowIntensityPercent);
            panel.BorderThickness = new Thickness(1);
            panel.Effect = CapsuleAppearanceMapper.BuildPanelShadowEffect(_capsuleConfig.ShadowPercent);
        }

        private void UpdateCapsuleGlowBrush(Color? accent)
        {
            var brush = CapsuleAppearanceMapper.BuildGlowBrush(_capsuleConfig.GlowIntensityPercent, accent);
            _capsuleGlowTransform = brush.RelativeTransform as TranslateTransform;
            CapsuleBorder.BorderBrush = brush;
            StartCapsuleGlowMarquee();
        }

        private void StartCapsuleGlowMarquee()
        {
            if (_capsuleGlowTransform == null)
            {
                return;
            }

            _capsuleGlowTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
            {
                From = -1,
                To = 1,
                Duration = CapsuleAppearanceMapper.MapGlowDuration(_capsuleConfig.GlowSpeedPercent),
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            });
        }

        private void ApplyCapsuleThickness()
        {
            if (_currentLayoutMetrics.CapsuleHeight <= 0)
            {
                return;
            }

            var height = CapsuleAppearanceMapper.MapCapsuleHeight(
                _currentLayoutMetrics.CapsuleHeight,
                _capsuleConfig.CapsuleThicknessPercent);
            CapsuleBorder.Height = height;
            UpdateCapsuleCornerRadius(height);
        }

        #region ClockAndBattery

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        private void AppsRefreshTimer_Tick(object? sender, EventArgs e)
        {
            UpdateRunningAppsRefreshInterval();
            RefreshRunningAppsBar();
        }

        private bool IsRunningAppsRefreshInteractive()
        {
            return _hoveredApp != null
                || CapsuleBorder.IsMouseOver
                || AppsPopup.IsOpen
                || OverflowAppsPopup.IsOpen;
        }

        private void UpdateRunningAppsRefreshInterval()
        {
            var interval = RunningAppsRefreshPolicy.GetInterval(IsRunningAppsRefreshInteractive());
            if (_appsRefreshTimer.Interval != interval)
            {
                _appsRefreshTimer.Interval = interval;
            }
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            ClockText.Text = now.ToString("HH:mm");
            DateText.Text = now.ToString("M月d日 ddd");
        }

        private void UpdateBatteryStatus()
        {
            var pct = SystemInfoService.GetBatteryPercent();
            if (pct >= 0)
            {
                BatteryPercentText.Text = pct == 100 ? string.Empty : $"{pct}%";
                BatteryIcon.ToolTip = SystemInfoService.GetBatteryInfo();
            }
        }

        #endregion

        #region GlowAnimation

        private void InitGlowAnimation()
        {
            _glowSpinStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var overlayDashAnim = new DoubleAnimation
            {
                From = 0,
                To = -92,
                Duration = TimeSpan.FromSeconds(1.45)
            };
            Storyboard.SetTarget(overlayDashAnim, AppHoverOverlayGlow);
            Storyboard.SetTargetProperty(overlayDashAnim, new PropertyPath(Shape.StrokeDashOffsetProperty));
            _glowSpinStoryboard.Children.Add(overlayDashAnim);
            _glowSpinStoryboard.Begin();
        }

        private void ShowGlow(RunningAppEntry? app = null)
        {
            if (app != null)
            {
                ApplyGlowAccent(app);
            }

            _glowStopTimer.Stop();
        }

        private void ScheduleGlowHide()
        {
            _glowStopTimer.Stop();
            _glowStopTimer.Start();
        }

        private void InitDockAnimations()
        {
            _dockExpandStoryboard = new Storyboard();
            var heightAnim = new DoubleAnimation
            {
                To = 76,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.8 }
            };
            Storyboard.SetTarget(heightAnim, CapsuleBorder);
            Storyboard.SetTargetProperty(heightAnim, new PropertyPath(Border.HeightProperty));
            _dockExpandStoryboard.Children.Add(heightAnim);

            _dockCollapseStoryboard = new Storyboard();
            var heightAnimBack = new DoubleAnimation
            {
                To = 64,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseInOut, Amplitude = 0.4 }
            };
            Storyboard.SetTarget(heightAnimBack, CapsuleBorder);
            Storyboard.SetTargetProperty(heightAnimBack, new PropertyPath(Border.HeightProperty));
            _dockCollapseStoryboard.Children.Add(heightAnimBack);
        }

        private void DockItem_MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void DockItem_MouseLeave(object sender, MouseEventArgs e)
        {
        }

        private void GlowStopTimer_Tick(object? sender, EventArgs e)
        {
            _glowStopTimer.Stop();
            UpdateCapsuleGlowBrush(null);
        }

        private void ExpandCapsuleForHover()
        {
            _capsuleHoverCollapseTimer.Stop();
            AnimateCapsuleHeight(GetBaseCapsuleHeight() + 12, TimeSpan.FromMilliseconds(230), new BackEase
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 0.45
            });
        }

        private void ScheduleCapsuleHoverCollapse()
        {
            _capsuleHoverCollapseTimer.Stop();
            _capsuleHoverCollapseTimer.Start();
        }

        private void CapsuleHoverCollapseTimer_Tick(object? sender, EventArgs e)
        {
            _capsuleHoverCollapseTimer.Stop();
            AnimateCapsuleHeight(GetBaseCapsuleHeight(), TimeSpan.FromMilliseconds(260), new QuadraticEase
            {
                EasingMode = EasingMode.EaseInOut
            });
        }

        private double GetBaseCapsuleHeight()
        {
            return _currentLayoutMetrics.CapsuleHeight > 0
                ? CapsuleAppearanceMapper.MapCapsuleHeight(_currentLayoutMetrics.CapsuleHeight, _capsuleConfig.CapsuleThicknessPercent)
                : CapsuleBorder.Height;
        }

        private void AnimateCapsuleHeight(double targetHeight, TimeSpan duration, IEasingFunction easing)
        {
            UpdateCapsuleCornerRadius(targetHeight);

            var animation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = duration,
                EasingFunction = easing
            };
            CapsuleBorder.BeginAnimation(HeightProperty, animation);
        }

        #endregion

        #region RunningApps

        private void RefreshRunningAppsBar()
        {
            RequestPermission(
                AppPermission.RunningApps,
                "后台列表权限",
                "允许读取当前运行中的窗口列表，用于显示胶囊任务栏和应用管理面板。",
                RefreshRunningAppsBarCore);
        }

        private void RefreshRunningAppsBarCore()
        {
            var refreshVersion = ++_runningAppsRefreshVersion;
            Task.Run(() =>
            {
                var windows = WindowManager.GetVisibleWindows()
                    .Where(window => !IsSelfWindow(window))
                    .ToList();

                Dispatcher.BeginInvoke(() =>
                {
                    if (refreshVersion != _runningAppsRefreshVersion)
                    {
                        return;
                    }

                    ApplyRunningAppsRefresh(windows);
                });
            });
        }

        private void ApplyRunningAppsRefresh(IReadOnlyList<WindowManager.WindowInfo> windows)
        {
            var candidates = new List<WindowAppCandidate>();
            var configDirty = false;
            foreach (var window in windows)
            {
                var appId = NormalizeAppId(window.ExecutablePath, window.ProcessName);
                if (string.IsNullOrWhiteSpace(appId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(window.ExecutablePath)
                    && !_capsuleConfig.KnownLaunchPaths.ContainsKey(appId))
                {
                    CapsuleConfigMutator.SetKnownLaunchPath(_capsuleConfig, appId, window.ExecutablePath);
                    configDirty = true;
                }

                candidates.Add(new WindowAppCandidate(
                    Title: window.Title,
                    AppId: appId,
                    WindowHandle: window.Handle,
                    IsForeground: window.IsForeground,
                    ExePath: window.ExecutablePath,
                    ProcessId: window.ProcessId));
            }

            _runningAppsSnapshot = RunningAppsSnapshotBuilder.Build(
                candidates,
                _capsuleConfig,
                _currentLayoutMetrics.VisibleAppSlots);

            if (configDirty)
            {
                CapsuleConfigService.Save(_capsuleConfig);
            }

            RenderMainBarApps();
            if (AppsPopup.IsOpen)
            {
                RenderAppsManagementPanel();
            }

            if (OverflowAppsPopup.IsOpen)
            {
                RenderOverflowAppsPanel();
            }
        }

        private static bool IsSelfWindow(WindowManager.WindowInfo window)
        {
            if (window.ProcessName.Equals("DynamicIslandBar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(window.ExecutablePath)
                && window.ExecutablePath.EndsWith("DynamicIslandBar.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAppId(string? executablePath, string processName)
        {
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                return executablePath.Trim().ToLowerInvariant();
            }

            return processName.Trim().ToLowerInvariant();
        }

        private void RenderMainBarApps()
        {
            AppIconsHost.Children.Clear();
            foreach (var app in _runningAppsSnapshot.MainBarApps)
            {
                AppIconsHost.Children.Add(CreateAppIcon(app, 40, expandOnHover: true));
            }

            OverflowFolderButton.Visibility = _runningAppsSnapshot.HasOverflowFolder
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateActiveAppSummary(_hoveredApp ?? GetPrimarySummaryApp(), _hoveredApp == null ? "当前窗口" : "正在运行");
        }

        private void RenderOverflowAppsPanel()
        {
            OverflowAppsListPanel.Children.Clear();
            foreach (var app in _runningAppsSnapshot.OverflowApps)
            {
                OverflowAppsListPanel.Children.Add(CreateAppIcon(app, 44, expandOnHover: false));
            }
        }

        private void RenderAppsManagementPanel()
        {
            AppsListPanel.Children.Clear();

            AddSection("运行中应用", _runningAppsSnapshot.AllApps.Where(app => app.IsRunning).ToList());
            AddSection("已隐藏应用", _runningAppsSnapshot.AllApps.Where(app => app.IsHiddenInCapsule).ToList());
            AddSection("喜好应用", _runningAppsSnapshot.AllApps.Where(app => app.IsFavorite).ToList());

            if (AppsListPanel.Children.Count == 0)
            {
                AppsListPanel.Children.Add(new TextBlock
                {
                    Text = "暂无应用",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    Margin = new Thickness(4, 8, 4, 8)
                });
            }
        }

        private void AddSection(string title, IReadOnlyList<RunningAppEntry> apps)
        {
            if (apps.Count == 0)
            {
                return;
            }

            AppsListPanel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 8, 4, 6)
            });

            foreach (var app in apps)
            {
                AppsListPanel.Children.Add(CreateAppListRow(app));
            }
        }

        private Border CreateAppListRow(RunningAppEntry app)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                Tag = app
            };

            var dock = new DockPanel { LastChildFill = true };
            var icon = BuildAppIconVisual(app, 18);
            DockPanel.SetDock(icon, Dock.Left);
            dock.Children.Add(icon);

            var title = new TextBlock
            {
                Text = app.DisplayName,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Opacity = app.IsRunning ? 1 : 0.65,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            dock.Children.Add(title);

            row.Child = dock;
            row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            row.MouseLeave += (_, _) => row.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
            row.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                HandleAppPrimaryAction(app);
            };
            row.MouseRightButtonDown += (_, e) =>
            {
                e.Handled = true;
                OpenAppContextMenu(row, app);
            };

            return row;
        }

        private Border CreateAppIcon(RunningAppEntry app, double size, bool expandOnHover)
        {
            var border = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Tag = app,
                Opacity = app.IsRunning ? 1 : 0.55,
                ClipToBounds = true,
                Child = BuildAppIconVisual(app, size * 0.48)
            };

            border.MouseEnter += (_, _) =>
            {
                var accent = GetAppAccentColor(app);
                border.Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(92, accent.R, accent.G, accent.B));
                _hoveredApp = app;
                ExpandCapsuleForHover();
                ShowGlow(app);

                if (expandOnHover)
                {
                    ShowAppHoverOverlay(border, app);
                }

                UpdateRunningAppsRefreshInterval();
            };
            border.MouseLeave += (_, _) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
                if (ReferenceEquals(_hoveredApp, app) || _hoveredApp?.AppId == app.AppId)
                {
                    _hoveredApp = null;
                }

                UpdateActiveAppSummary(GetPrimarySummaryApp(), "当前窗口");
                ScheduleCapsuleHoverCollapse();
                ScheduleGlowHide();

                if (expandOnHover)
                {
                    HideAppHoverOverlay(app);
                }

                UpdateRunningAppsRefreshInterval();
            };
            border.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                HandleAppPrimaryAction(app);
            };
            border.MouseRightButtonDown += (_, e) =>
            {
                e.Handled = true;
                OpenAppContextMenu(border, app);
            };

            return border;
        }

        private void ShowAppHoverOverlay(FrameworkElement iconHost, RunningAppEntry app)
        {
            var position = iconHost.TransformToAncestor(CapsuleGrid).Transform(new Point(0, 0));
            var frame = AppHoverOverlayLayoutPolicy.GetOverlayFrame(
                position.X,
                position.Y,
                iconHost.ActualWidth > 0 ? iconHost.ActualWidth : iconHost.Width,
                iconHost.ActualHeight > 0 ? iconHost.ActualHeight : iconHost.Height,
                AppHoverOverlayPanel.Width,
                AppHoverOverlayPanel.Height,
                AppHoverOverlayLayer.ActualWidth > 0 ? AppHoverOverlayLayer.ActualWidth : CapsuleGrid.Width,
                AppHoverOverlayLayer.ActualHeight > 0 ? AppHoverOverlayLayer.ActualHeight : CapsuleGrid.Height);

            Canvas.SetLeft(AppHoverOverlayPanel, frame.Left);
            Canvas.SetTop(AppHoverOverlayPanel, frame.Top);

            var accent = GetAppAccentColor(app);
            AppHoverOverlayIcon.Content = BuildAppIconVisual(app, 18);
            AppHoverOverlayTitle.Text = app.DisplayName;
            AppHoverOverlayStatus.Text = app.IsRunning ? "正在运行" : "已固定";
            AppHoverOverlayGlow.Stroke = new SolidColorBrush(accent);
            AppHoverOverlayBackground.BorderBrush = new SolidColorBrush(Color.FromArgb(72, accent.R, accent.G, accent.B));

            AppHoverOverlayPanel.Visibility = Visibility.Visible;
            AppHoverOverlayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
            AppHoverOverlayScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            AppHoverOverlayScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private void HideAppHoverOverlay(RunningAppEntry app)
        {
            if (_hoveredApp != null && !string.Equals(_hoveredApp.AppId, app.AppId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AppHoverOverlayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(90),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            AppHoverOverlayScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = 0.94,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
            AppHoverOverlayScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                To = 0.94,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            });
        }

        private RunningAppEntry? GetPrimarySummaryApp()
        {
            return _runningAppsSnapshot.MainBarApps.FirstOrDefault(app => app.IsForeground)
                ?? _runningAppsSnapshot.AllApps.FirstOrDefault(app => app.IsForeground)
                ?? _runningAppsSnapshot.MainBarApps.FirstOrDefault()
                ?? _runningAppsSnapshot.AllApps.FirstOrDefault(app => app.IsRunning);
        }

        private void UpdateActiveAppSummary(RunningAppEntry? app, string status)
        {
            if (app == null)
            {
                ActiveAppSummaryPanel.Visibility = Visibility.Collapsed;
                ActiveAppSummaryIcon.Content = null;
                ActiveAppSummaryTitle.Text = string.Empty;
                ActiveAppSummarySubtitle.Text = string.Empty;
                return;
            }

            ActiveAppSummaryPanel.Visibility = Visibility.Visible;
            ActiveAppSummaryIcon.Content = BuildAppIconVisual(app, 20);
            ActiveAppSummaryTitle.Text = app.DisplayName;
            ActiveAppSummarySubtitle.Text = status;
            ApplyGlowAccent(app);
        }

        private FrameworkElement BuildAppIconVisual(RunningAppEntry app, double iconSize)
        {
            var iconSource = GetIconSource(app.ExePath);
            if (iconSource != null)
            {
                return new Image
                {
                    Source = iconSource,
                    Width = iconSize,
                    Height = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            return new TextBlock
            {
                Text = GetFallbackIconGlyph(app.DisplayName),
                Foreground = Brushes.White,
                FontSize = Math.Max(iconSize * 0.55, 12),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private ImageSource? GetIconSource(string? exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                return null;
            }

            if (_iconCache.TryGetValue(exePath, out var cached))
            {
                return cached;
            }

            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon == null)
                {
                    return null;
                }

                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                source.Freeze();
                _iconCache[exePath] = source;
                return source;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyGlowAccent(RunningAppEntry app)
        {
            var accent = GetAppAccentColor(app);
            UpdateCapsuleGlowBrush(accent);
            ActiveAppSummaryPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(168, accent.R, accent.G, accent.B));
        }

        private Color GetAppAccentColor(RunningAppEntry app)
        {
            var cacheKey = !string.IsNullOrWhiteSpace(app.ExePath) ? app.ExePath : app.AppId;
            if (_accentCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var iconSource = GetIconSource(app.ExePath);
            var color = iconSource is BitmapSource bitmap
                ? ExtractAccentColor(bitmap, app.DisplayName)
                : BuildFallbackAccentColor(app.DisplayName);
            _accentCache[cacheKey] = color;
            return color;
        }

        private static Color ExtractAccentColor(BitmapSource source, string fallbackSeed)
        {
            try
            {
                var bitmap = source.Format == PixelFormats.Bgra32
                    ? source
                    : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

                var width = bitmap.PixelWidth;
                var height = bitmap.PixelHeight;
                var stride = width * 4;
                var pixels = new byte[stride * height];
                bitmap.CopyPixels(pixels, stride, 0);

                double redTotal = 0;
                double greenTotal = 0;
                double blueTotal = 0;
                double weightTotal = 0;
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    var blue = pixels[i];
                    var green = pixels[i + 1];
                    var red = pixels[i + 2];
                    var alpha = pixels[i + 3];
                    if (alpha < 48)
                    {
                        continue;
                    }

                    var max = Math.Max(red, Math.Max(green, blue));
                    var min = Math.Min(red, Math.Min(green, blue));
                    var saturation = max - min;
                    if (max < 48 || saturation < 18)
                    {
                        continue;
                    }

                    var weight = (alpha / 255.0) * (1 + saturation / 64.0);
                    redTotal += red * weight;
                    greenTotal += green * weight;
                    blueTotal += blue * weight;
                    weightTotal += weight;
                }

                if (weightTotal <= 0)
                {
                    return BuildFallbackAccentColor(fallbackSeed);
                }

                return BoostAccent(Color.FromRgb(
                    (byte)Math.Clamp((int)Math.Round(redTotal / weightTotal), 0, 255),
                    (byte)Math.Clamp((int)Math.Round(greenTotal / weightTotal), 0, 255),
                    (byte)Math.Clamp((int)Math.Round(blueTotal / weightTotal), 0, 255)));
            }
            catch
            {
                return BuildFallbackAccentColor(fallbackSeed);
            }
        }

        private static Color BuildFallbackAccentColor(string seed)
        {
            var hash = seed.Aggregate(17, (current, ch) => (current * 31) + ch);
            var palette = new[]
            {
                Color.FromRgb(76, 217, 100),
                Color.FromRgb(0, 122, 255),
                Color.FromRgb(255, 45, 85),
                Color.FromRgb(255, 149, 0),
                Color.FromRgb(90, 200, 250)
            };

            return palette[Math.Abs(hash) % palette.Length];
        }

        private static Color BoostAccent(Color color)
        {
            return Color.FromRgb(
                (byte)Math.Clamp(color.R + 18, 0, 255),
                (byte)Math.Clamp(color.G + 18, 0, 255),
                (byte)Math.Clamp(color.B + 18, 0, 255));
        }

        private static string GetFallbackIconGlyph(string displayName)
        {
            var trimmed = displayName.Trim();
            return string.IsNullOrEmpty(trimmed) ? "●" : trimmed[..1].ToUpperInvariant();
        }

        private void HandleAppPrimaryAction(RunningAppEntry app)
        {
            HidePermissionPrompt();
            CloseAllPanels();

            switch (AppPrimaryActionResolver.Resolve(app, GetRecentlyActivatedAppId()))
            {
                case AppPrimaryAction.Minimize:
                    WindowManager.MinimizeWindow(app.RepresentativeWindowHandle);
                    ClearRecentPrimaryActivation(app.AppId);
                    return;
                case AppPrimaryAction.ActivateOrLaunch:
                default:
                    if (app.IsRunning)
                    {
                        if (WindowManager.ActivateWindow(app.RepresentativeWindowHandle))
                        {
                            TrackRecentPrimaryActivation(app.AppId);
                        }

                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(app.ExePath))
                    {
                        TryLaunchApp(app.ExePath);
                        TrackRecentPrimaryActivation(app.AppId);
                    }

                    return;
            }
        }

        private string? GetRecentlyActivatedAppId()
        {
            if (string.IsNullOrWhiteSpace(_lastPrimaryActivatedAppId))
            {
                return null;
            }

            return DateTime.UtcNow - _lastPrimaryActivatedAtUtc <= TimeSpan.FromSeconds(2)
                ? _lastPrimaryActivatedAppId
                : null;
        }

        private void TrackRecentPrimaryActivation(string appId)
        {
            _lastPrimaryActivatedAppId = appId;
            _lastPrimaryActivatedAtUtc = DateTime.UtcNow;
        }

        private void ClearRecentPrimaryActivation(string appId)
        {
            if (string.Equals(_lastPrimaryActivatedAppId, appId, StringComparison.OrdinalIgnoreCase))
            {
                _lastPrimaryActivatedAppId = null;
                _lastPrimaryActivatedAtUtc = default;
            }
        }

        private void OpenAppContextMenu(FrameworkElement host, RunningAppEntry app)
        {
            var state = AppsMenuStateBuilder.Build(app);
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White
            };

            var visibilityItem = new MenuItem
            {
                Header = state.CanShowInCapsule ? "显示到胶囊" : "隐藏图标",
                Foreground = Brushes.White
            };
            visibilityItem.Click += (_, _) =>
            {
                CapsuleConfigMutator.SetHidden(_capsuleConfig, app.AppId, !app.IsHiddenInCapsule);
                CapsuleConfigService.Save(_capsuleConfig);
                RefreshRunningAppsBarCore();
            };

            var runItem = new MenuItem
            {
                Header = state.CanCloseApp ? "关闭应用" : "打开应用",
                Foreground = Brushes.White,
                IsEnabled = state.CanCloseApp || state.CanOpenApp
            };
            runItem.Click += (_, _) =>
            {
                if (app.IsRunning)
                {
                    WindowManager.CloseProcess(app.RepresentativeProcessId);
                }
                else if (!string.IsNullOrWhiteSpace(app.ExePath))
                {
                    TryLaunchApp(app.ExePath);
                }

                RefreshRunningAppsBar();
            };

            var favoriteItem = new MenuItem
            {
                Header = app.IsFavorite ? "取消喜好" : "添加到喜好",
                Foreground = Brushes.White
            };
            favoriteItem.Click += (_, _) =>
            {
                CapsuleConfigMutator.SetFavorite(_capsuleConfig, app.AppId, !app.IsFavorite);
                CapsuleConfigService.Save(_capsuleConfig);
                RefreshRunningAppsBarCore();
            };

            menu.Items.Add(visibilityItem);
            menu.Items.Add(runItem);
            menu.Items.Add(favoriteItem);

            StyleCapsuleContextMenu(menu);
            host.ContextMenu = menu;
            menu.IsOpen = true;
        }

        private static void TryLaunchApp(string exePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        #endregion

        #region ContextMenu

        private void Capsule_RightClick(object sender, MouseButtonEventArgs e)
        {
            var menu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White
            };

            var settingsMenu = new MenuItem { Header = "设置", Foreground = Brushes.White };

            var themeMenu = new MenuItem { Header = "风格", Foreground = Brushes.White };
            AddThemeMenuItem(themeMenu, CapsuleThemePreset.ClassicDark, "ClassicDark");
            AddThemeMenuItem(themeMenu, CapsuleThemePreset.GlassGreen, "GlassGreen");
            AddThemeMenuItem(themeMenu, CapsuleThemePreset.SoftLight, "SoftLight");

            var appearanceMenu = new MenuItem { Header = "外观", Foreground = Brushes.White };
            appearanceMenu.Items.Add(CreateAppearanceSliderItem(
                "透明度",
                _capsuleConfig.GlassOpacityPercent,
                value => CapsuleConfigMutator.SetGlassOpacityPercent(_capsuleConfig, value)));
            appearanceMenu.Items.Add(CreateAppearanceSliderItem(
                "阴影",
                _capsuleConfig.ShadowPercent,
                value => CapsuleConfigMutator.SetShadowPercent(_capsuleConfig, value)));
            appearanceMenu.Items.Add(CreateAppearanceSliderItem(
                "胶囊粗细",
                _capsuleConfig.CapsuleThicknessPercent,
                value => CapsuleConfigMutator.SetCapsuleThicknessPercent(_capsuleConfig, value)));
            appearanceMenu.Items.Add(CreateAppearanceSliderItem(
                "胶囊长度",
                _capsuleConfig.CapsuleLengthPercent,
                value => CapsuleConfigMutator.SetCapsuleLengthPercent(_capsuleConfig, value),
                refreshLayout: true));

            var glowMenu = new MenuItem { Header = "流光", Foreground = Brushes.White };
            glowMenu.Items.Add(CreateAppearanceSliderItem(
                "亮度",
                _capsuleConfig.GlowIntensityPercent,
                value => CapsuleConfigMutator.SetGlowIntensityPercent(_capsuleConfig, value)));
            glowMenu.Items.Add(CreateAppearanceSliderItem(
                "粗细",
                _capsuleConfig.GlowThicknessPercent,
                value => CapsuleConfigMutator.SetGlowThicknessPercent(_capsuleConfig, value)));
            glowMenu.Items.Add(CreateAppearanceSliderItem(
                "速度",
                _capsuleConfig.GlowSpeedPercent,
                value => CapsuleConfigMutator.SetGlowSpeedPercent(_capsuleConfig, value)));

            settingsMenu.Items.Add(themeMenu);
            settingsMenu.Items.Add(appearanceMenu);
            settingsMenu.Items.Add(glowMenu);

            var hideTaskbar = new MenuItem { Header = "隐藏系统任务栏", Foreground = Brushes.White };
            hideTaskbar.Click += (_, _) => TaskbarManager.Hide();

            var showTaskbar = new MenuItem { Header = "显示系统任务栏", Foreground = Brushes.White };
            showTaskbar.Click += (_, _) => TaskbarManager.Show();

            var exitItem = new MenuItem { Header = "退出程序", Foreground = Brushes.White };
            exitItem.Click += (_, _) =>
            {
                TaskbarManager.Show();
                Application.Current.Shutdown();
            };

            menu.Items.Add(settingsMenu);
            menu.Items.Add(new Separator());
            menu.Items.Add(hideTaskbar);
            menu.Items.Add(showTaskbar);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            if (sender is FrameworkElement host)
            {
                StyleCapsuleContextMenu(menu);
                host.ContextMenu = menu;
                menu.IsOpen = true;
            }
        }

        private void StyleCapsuleContextMenu(ContextMenu menu)
        {
            menu.Style = (Style)FindResource("CapsuleContextMenuStyle");
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                StyleCapsuleMenuItem(item);
            }
        }

        private void StyleCapsuleMenuItem(MenuItem item)
        {
            item.Style = (Style)FindResource("CapsuleMenuItemStyle");
            item.Foreground = Brushes.White;
            foreach (var child in item.Items.OfType<MenuItem>())
            {
                StyleCapsuleMenuItem(child);
            }
        }

        private MenuItem CreateAppearanceSliderItem(
            string title,
            int initialValue,
            Action<int> updateConfig,
            bool refreshLayout = false)
        {
            var valueText = new TextBlock
            {
                Text = $"{initialValue}%",
                Foreground = Brushes.White,
                Width = 42,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Width = 150,
                Value = initialValue,
                IsMoveToPointEnabled = true,
                TickFrequency = 1,
                SmallChange = 1,
                LargeChange = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            slider.ValueChanged += (_, args) =>
            {
                var percent = (int)Math.Round(args.NewValue);
                valueText.Text = $"{percent}%";
                ApplyAppearanceSliderValue(updateConfig, percent, refreshLayout);
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2, 4, 2, 4)
            };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                Width = 54,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(slider);
            panel.Children.Add(valueText);

            return new MenuItem
            {
                Header = panel,
                StaysOpenOnClick = true,
                Foreground = Brushes.White
            };
        }

        private void ApplyAppearanceSliderValue(Action<int> updateConfig, int percent, bool refreshLayout)
        {
            updateConfig(percent);
            CapsuleConfigService.Save(_capsuleConfig);
            if (refreshLayout)
            {
                ApplyLayout();
                RefreshRunningAppsBar();
            }

            ApplyTheme();
        }

        private void AddThemeMenuItem(MenuItem parent, CapsuleThemePreset preset, string title)
        {
            var item = new MenuItem
            {
                Header = title,
                Foreground = Brushes.White,
                IsCheckable = true,
                IsChecked = _capsuleConfig.ThemePreset == preset
            };
            item.Click += (_, _) =>
            {
                CapsuleConfigMutator.SetThemePreset(_capsuleConfig, preset);
                CapsuleConfigService.Save(_capsuleConfig);
                ApplyTheme();
                RenderMainBarApps();
                RenderOverflowAppsPanel();
                RenderAppsManagementPanel();
            };
            parent.Items.Add(item);
        }

        #endregion

        #region SystemIcons

        private void SystemIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                ExpandCapsuleForHover();
                SetSystemIconHighlight(border, true);
                if (border.Name == nameof(BatteryIcon))
                {
                    border.ToolTip = SystemInfoService.GetBatteryInfo();
                }
            }
        }

        private void SystemIcon_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                SetSystemIconHighlight(border, false);
                ScheduleCapsuleHoverCollapse();
            }
        }

        private void WifiIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandCapsuleForHover();
            SetSystemIconHighlight(WifiIcon, true);
            ShowHoverPopup(GetWifiPopupState(), RefreshWifiPanel);
        }

        private void WifiIcon_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(WifiIcon, false);
            ScheduleCapsuleHoverCollapse();
            ScheduleHoverPopupClose(GetWifiPopupState());
        }

        private void VolumeIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandCapsuleForHover();
            SetSystemIconHighlight(VolumeIcon, true);
            ShowHoverPopup(GetVolumePopupState(), RefreshVolumePanel);
        }

        private void VolumeIcon_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(VolumeIcon, false);
            ScheduleCapsuleHoverCollapse();
            ScheduleHoverPopupClose(GetVolumePopupState());
        }

        private void AppsButton_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandCapsuleForHover();
            SetSystemIconHighlight(AppsButton, true);
            ShowHoverPopup(GetAppsPopupState(), RefreshAppsList);
        }

        private void AppsButton_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(AppsButton, false);
            ScheduleCapsuleHoverCollapse();
            ScheduleHoverPopupClose(GetAppsPopupState());
        }

        private void OverflowFolderButton_MouseEnter(object sender, MouseEventArgs e)
        {
            ExpandCapsuleForHover();
            SetSystemIconHighlight(OverflowFolderButton, true);
            ShowHoverPopup(GetOverflowPopupState(), RefreshOverflowAppsPanel);
        }

        private void OverflowFolderButton_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(OverflowFolderButton, false);
            ScheduleCapsuleHoverCollapse();
            ScheduleHoverPopupClose(GetOverflowPopupState());
        }

        private void WifiPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            CancelHoverPopupClose();
            SetSystemIconHighlight(WifiIcon, true);
        }

        private void WifiPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(WifiIcon, false);
            ScheduleHoverPopupClose(GetWifiPopupState());
        }

        private void VolumePanel_MouseEnter(object sender, MouseEventArgs e)
        {
            CancelHoverPopupClose();
            SetSystemIconHighlight(VolumeIcon, true);
        }

        private void VolumePanel_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(VolumeIcon, false);
            ScheduleHoverPopupClose(GetVolumePopupState());
        }

        private void AppsPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            CancelHoverPopupClose();
            SetSystemIconHighlight(AppsButton, true);
        }

        private void AppsPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(AppsButton, false);
            ScheduleHoverPopupClose(GetAppsPopupState());
        }

        private void OverflowAppsPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            CancelHoverPopupClose();
            SetSystemIconHighlight(OverflowFolderButton, true);
        }

        private void OverflowAppsPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(OverflowFolderButton, false);
            ScheduleHoverPopupClose(GetOverflowPopupState());
        }

        private void SetSystemIconHighlight(Border border, bool highlighted)
        {
            border.Background = new SolidColorBrush(
                highlighted
                    ? Color.FromArgb(42, 255, 255, 255)
                    : Color.FromArgb(10, 255, 255, 255));
            border.BorderBrush = new SolidColorBrush(
                highlighted
                    ? Color.FromArgb(48, 76, 217, 100)
                    : Color.FromArgb(12, 255, 255, 255));
        }

        private PopupState GetWifiPopupState() => new()
        {
            Icon = WifiIcon,
            Panel = WifiPanel,
            Popup = WifiPopup
        };

        private PopupState GetVolumePopupState() => new()
        {
            Icon = VolumeIcon,
            Panel = VolumePanel,
            Popup = VolumePopup
        };

        private PopupState GetAppsPopupState() => new()
        {
            Icon = AppsButton,
            Panel = AppsPanel,
            Popup = AppsPopup
        };

        private PopupState GetOverflowPopupState() => new()
        {
            Icon = OverflowFolderButton,
            Panel = OverflowAppsPanel,
            Popup = OverflowAppsPopup
        };

        private void ShowHoverPopup(PopupState popupState, Action refreshAction)
        {
            CancelHoverPopupClose();
            if (!popupState.Popup.IsOpen)
            {
                CloseAllPanels();
                popupState.Popup.IsOpen = true;
            }

            UpdateRunningAppsRefreshInterval();
            refreshAction();
        }

        private void ScheduleHoverPopupClose(PopupState popupState)
        {
            _pendingHoverClosePopup = popupState;
            _hoverCloseTimer.Stop();
            _hoverCloseTimer.Start();
        }

        private void CancelHoverPopupClose()
        {
            _pendingHoverClosePopup = null;
            _hoverCloseTimer.Stop();
        }

        private void HoverCloseTimer_Tick(object? sender, EventArgs e)
        {
            _hoverCloseTimer.Stop();
            if (_pendingHoverClosePopup == null)
            {
                return;
            }

            if (_pendingHoverClosePopup.Icon.IsMouseOver || _pendingHoverClosePopup.Panel.IsMouseOver)
            {
                return;
            }

            _pendingHoverClosePopup.Popup.IsOpen = false;
            _pendingHoverClosePopup = null;
            UpdateRunningAppsRefreshInterval();
        }

        private void WifiIcon_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenWifiSettings();
        }

        private void VolumeIcon_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenSoundSettings();
        }

        private void BatteryIcon_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            HidePermissionPrompt();
            SystemInfoService.OpenBatterySettings();
        }

        private void AppsButton_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenTaskManager();
        }

        private void Popup_Closed(object sender, EventArgs e)
        {
            CancelHoverPopupClose();
            SetSystemIconHighlight(WifiIcon, WifiIcon.IsMouseOver || WifiPanel.IsMouseOver);
            SetSystemIconHighlight(VolumeIcon, VolumeIcon.IsMouseOver || VolumePanel.IsMouseOver);
            SetSystemIconHighlight(AppsButton, AppsButton.IsMouseOver || AppsPanel.IsMouseOver);
            SetSystemIconHighlight(OverflowFolderButton, OverflowFolderButton.IsMouseOver || OverflowAppsPanel.IsMouseOver);
            _wifiRefreshVersion++;
            _volumeRefreshVersion++;
            UpdateRunningAppsRefreshInterval();
        }

        private void RefreshAppsList()
        {
            RefreshRunningAppsBar();
        }

        private void RefreshOverflowAppsPanel()
        {
            RenderOverflowAppsPanel();
        }

        #endregion

        #region DragSnap

        private void Capsule_DragStart(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || ShouldSuppressDragStart(e.OriginalSource as DependencyObject))
            {
                return;
            }

            _isDraggingCapsule = true;
            _dragStartPoint = PointToScreen(e.GetPosition(this));
            _dragStartLeft = Left;
            _dragStartTop = Top;
            Mouse.Capture(CapsuleBorder, CaptureMode.SubTree);
            e.Handled = true;
        }

        private void Capsule_DragMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingCapsule)
            {
                return;
            }

            var currentPoint = PointToScreen(e.GetPosition(this));
            Left = _dragStartLeft + (currentPoint.X - _dragStartPoint.X);
            Top = _dragStartTop + (currentPoint.Y - _dragStartPoint.Y);
        }

        private void Capsule_DragEnd(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingCapsule)
            {
                return;
            }

            _isDraggingCapsule = false;
            Mouse.Capture(null);
            e.Handled = true;

            var resolvedMode = CapsuleLayoutManager.ResolveDropMode(
                DisplayBoundsProvider.GetPrimaryScreenSize().Height,
                Top,
                _capsuleConfig.Mode);

            if (resolvedMode != _capsuleConfig.Mode)
            {
                CapsuleConfigMutator.SetMode(_capsuleConfig, resolvedMode);
                CapsuleConfigService.Save(_capsuleConfig);
            }

            ApplyLayout();
            RefreshRunningAppsBarCore();
        }

        private bool ShouldSuppressDragStart(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button or Slider)
                {
                    return true;
                }

                if (source is FrameworkElement element)
                {
                    if (element.Tag is RunningAppEntry)
                    {
                        return true;
                    }

                    if (ReferenceEquals(element, WifiIcon)
                        || ReferenceEquals(element, VolumeIcon)
                        || ReferenceEquals(element, BatteryIcon)
                        || ReferenceEquals(element, AppsButton)
                        || ReferenceEquals(element, OverflowFolderButton))
                    {
                        return true;
                    }
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        #endregion

        #region WifiPanel

        private void RefreshWifiPanel()
        {
            RequestPermission(
                AppPermission.WifiNearbyNetworks,
                "WiFi 信息权限",
                "允许读取附近 WiFi 和已保存网络，用于显示 WLAN 详细面板。",
                RefreshWifiPanelCore);
        }

        private void RefreshWifiPanelCore()
        {
            var refreshVersion = ++_wifiRefreshVersion;
            WifiNetworksList.Children.Clear();
            var currentSsid = WifiService.GetCurrentSsid();
            WifiSystemAccessHint.Visibility = Visibility.Collapsed;
            WifiSystemAccessHint.Text = string.Empty;
            WifiMoreSettingsButton.Content = "更多 WiFi 设置";

            if (!string.IsNullOrEmpty(currentSsid))
            {
                WifiCurrentNetwork.Visibility = Visibility.Visible;
                WifiCurrentSsid.Text = currentSsid;
                WifiStatusText.Text = "已连接";
            }
            else
            {
                WifiCurrentNetwork.Visibility = Visibility.Collapsed;
                WifiStatusText.Text = "未连接";
            }

            WifiNetworksList.Children.Add(new TextBlock
            {
                Text = "正在加载 WiFi 列表...",
                Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 8)
            });

            Task.Run(() =>
            {
                var snapshot = WifiService.GetNetworkSnapshot();
                Dispatcher.Invoke(() =>
                {
                    if (refreshVersion != _wifiRefreshVersion || !WifiPopup.IsOpen)
                    {
                        return;
                    }

                    _lastWifiAccessIssue = snapshot.AccessIssue;
                    ApplyWifiAccessIssue(snapshot.AccessIssue);

                    WifiNetworksList.Children.Clear();
                    if (snapshot.Networks.Count == 0)
                    {
                        WifiStatusText.Text = "无可用列表";
                        WifiNetworksList.Children.Add(new TextBlock
                        {
                            Text = GetWifiEmptyStateMessage(snapshot.AccessIssue),
                            Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 12,
                            Margin = new Thickness(0, 8, 0, 8)
                        });
                        return;
                    }

                    foreach (var net in snapshot.Networks)
                    {
                        var row = new Border
                        {
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(10, 8, 10, 8),
                            Margin = new Thickness(0, 2, 0, 2),
                            Cursor = Cursors.Hand,
                            Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255))
                        };
                        var panel = new DockPanel();
                        var nameBlock = new TextBlock
                        {
                            Text = net.Ssid,
                            Foreground = net.IsConnected ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                            FontSize = 13,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var signalBlock = new TextBlock
                        {
                            Text = net.SignalStrength,
                            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                            FontSize = 11,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        panel.Children.Add(nameBlock);
                        var rightPanel = new StackPanel { Orientation = Orientation.Horizontal };
                        rightPanel.Children.Add(signalBlock);
                        if (net.IsSecured)
                        {
                            rightPanel.Children.Add(new TextBlock
                            {
                                Text = " 🔒",
                                Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                                FontSize = 10,
                                VerticalAlignment = VerticalAlignment.Center
                            });
                        }
                        DockPanel.SetDock(rightPanel, Dock.Right);
                        panel.Children.Insert(0, rightPanel);
                        row.Child = panel;

                        var capturedNet = net;
                        row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                        row.MouseLeave += (_, _) => row.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        row.MouseLeftButtonDown += (_, e) =>
                        {
                            e.Handled = true;
                            if (!capturedNet.IsConnected)
                            {
                                RequestPermission(
                                    AppPermission.WifiControl,
                                    "WiFi 控制权限",
                                    $"允许切换到“{capturedNet.Ssid}”并管理当前 WiFi 连接。",
                                    () =>
                                    {
                                        WifiStatusText.Text = $"正在连接 {capturedNet.Ssid}";
                                        WifiService.Connect(capturedNet.Ssid);
                                        RefreshWifiPanelCore();
                                    });
                            }
                        };
                        WifiNetworksList.Children.Add(row);
                    }
                });
            });
        }

        private void WifiRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshWifiPanel();
        }

        private void WifiDisconnect_Click(object sender, RoutedEventArgs e)
        {
            RequestPermission(
                AppPermission.WifiControl,
                "WiFi 控制权限",
                "允许断开当前 WiFi 连接。",
                () =>
                {
                    WifiService.Disconnect();
                    RefreshWifiPanelCore();
                });
        }

        private void WifiMoreSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_lastWifiAccessIssue == WifiAccessIssue.LocationPermissionRequired)
            {
                SystemInfoService.OpenLocationPrivacySettings();
            }
            else
            {
                SystemInfoService.OpenWifiSettings();
            }

            CloseAllPanels();
            HidePermissionPrompt();
        }

        #endregion

        #region VolumePanel

        private void RefreshVolumePanel()
        {
            RequestPermission(
                AppPermission.AudioControl,
                "声音控制权限",
                "允许读取并控制系统音量与输出设备，用于显示声音面板并执行切换。",
                RefreshVolumePanelCore);
        }

        private void RefreshVolumePanelCore()
        {
            var refreshVersion = ++_volumeRefreshVersion;
            var vol = AudioService.GetVolume();
            if (vol >= 0)
            {
                _suppressVolumeEvent = true;
                VolumeSlider.Value = vol;
                VolumePercentText.Text = $"{vol}%";
                _suppressVolumeEvent = false;
            }

            var muted = AudioService.IsMuted();
            MuteBtn.Content = muted ? "取消静音" : "静音";
            MuteBtn.Background = muted
                ? new SolidColorBrush(Color.FromArgb(50, 76, 217, 100))
                : new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));

            AudioDevicesList.Children.Clear();
            AudioDevicesList.Children.Add(new TextBlock
            {
                Text = "正在读取输出设备...",
                Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0)
            });

            Task.Run(() =>
            {
                var devices = AudioService.GetOutputDevices();
                Dispatcher.Invoke(() =>
                {
                    if (refreshVersion != _volumeRefreshVersion || !VolumePopup.IsOpen)
                    {
                        return;
                    }

                    AudioDevicesList.Children.Clear();
                    foreach (var dev in devices)
                    {
                        var row = new Border
                        {
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10, 6, 10, 6),
                            Margin = new Thickness(0, 1, 0, 1),
                            Cursor = Cursors.Hand,
                            Background = dev.IsDefault
                                ? new SolidColorBrush(Color.FromArgb(25, 76, 217, 100))
                                : new SolidColorBrush(Color.FromArgb(0, 255, 255, 255))
                        };
                        var text = new TextBlock
                        {
                            Text = dev.Name + (dev.IsDefault ? " ✓" : string.Empty),
                            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                            FontSize = 12,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        row.Child = text;
                        row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                        row.MouseLeave += (_, _) => row.Background = dev.IsDefault
                            ? new SolidColorBrush(Color.FromArgb(25, 76, 217, 100))
                            : new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        row.MouseLeftButtonDown += (_, e) =>
                        {
                            e.Handled = true;
                            RequestPermission(
                                AppPermission.AudioControl,
                                "声音控制权限",
                                $"允许切换当前默认输出设备到“{dev.Name}”。",
                                () =>
                                {
                                    if (!AudioService.SwitchDevice(dev.Id))
                                    {
                                        SystemInfoService.OpenSoundSettings();
                                    }

                                    RefreshVolumePanelCore();
                                });
                        };
                        AudioDevicesList.Children.Add(row);
                    }
                });
            });
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressVolumeEvent || !_windowLoaded)
            {
                return;
            }

            var pct = (int)VolumeSlider.Value;
            VolumePercentText.Text = $"{pct}%";
            RequestPermission(
                AppPermission.AudioControl,
                "声音控制权限",
                "允许调整系统主音量。",
                () => AudioService.SetVolume(pct));
        }

        private void MuteBtn_Click(object sender, RoutedEventArgs e)
        {
            RequestPermission(
                AppPermission.AudioControl,
                "声音控制权限",
                "允许切换系统静音状态。",
                () =>
                {
                    AudioService.ToggleMute();
                    RefreshVolumePanelCore();
                });
        }

        private void VolumeMoreSettings_Click(object sender, RoutedEventArgs e)
        {
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenSoundSettings();
        }

        #endregion

        #region Permissions

        private void RequestPermission(AppPermission permission, string title, string message, Action grantedAction)
        {
            var result = PermissionService.Check(permission);
            if (result.IsGranted)
            {
                HidePermissionPrompt();
                grantedAction();
                return;
            }

            if (!result.ShouldPrompt)
            {
                HidePermissionPrompt();
                return;
            }

            ShowPermissionPrompt(permission, title, message, grantedAction);
        }

        private void ShowPermissionPrompt(AppPermission permission, string title, string message, Action grantedAction)
        {
            CloseAllPanels();
            PermissionPromptTitle.Text = title;
            PermissionPromptMessage.Text = message;
            PermissionPromptPanel.Visibility = Visibility.Visible;
            _pendingPermissionPrompt = new PermissionPromptState
            {
                Permission = permission,
                GrantedAction = grantedAction
            };
        }

        private void HidePermissionPrompt()
        {
            PermissionPromptPanel.Visibility = Visibility.Collapsed;
            _pendingPermissionPrompt = null;
        }

        private void PermissionAllowCurrent_Click(object sender, RoutedEventArgs e)
        {
            ApplyPermissionDecision(PermissionDecision.AllowCurrent);
        }

        private void PermissionDeny_Click(object sender, RoutedEventArgs e)
        {
            ApplyPermissionDecision(PermissionDecision.Deny);
        }

        private void PermissionAllowAll_Click(object sender, RoutedEventArgs e)
        {
            ApplyPermissionDecision(PermissionDecision.AllowAll);
        }

        private void ApplyPermissionDecision(PermissionDecision decision)
        {
            if (_pendingPermissionPrompt == null)
            {
                HidePermissionPrompt();
                return;
            }

            var prompt = _pendingPermissionPrompt;
            PermissionService.ApplyDecision(prompt.Permission, decision);
            HidePermissionPrompt();

            if (decision != PermissionDecision.Deny)
            {
                prompt.GrantedAction();
            }
        }

        private void ApplyWifiAccessIssue(WifiAccessIssue accessIssue)
        {
            switch (accessIssue)
            {
                case WifiAccessIssue.LocationPermissionRequired:
                    WifiSystemAccessHint.Text = "需要 Windows 定位权限";
                    WifiSystemAccessHint.Visibility = Visibility.Visible;
                    WifiMoreSettingsButton.Content = "打开定位权限设置";
                    break;
                case WifiAccessIssue.ElevatedAccessRequired:
                    WifiSystemAccessHint.Text = "需要管理员权限";
                    WifiSystemAccessHint.Visibility = Visibility.Visible;
                    WifiMoreSettingsButton.Content = "更多 WiFi 设置";
                    break;
                default:
                    WifiSystemAccessHint.Text = string.Empty;
                    WifiSystemAccessHint.Visibility = Visibility.Collapsed;
                    WifiMoreSettingsButton.Content = "更多 WiFi 设置";
                    break;
            }
        }

        private static string GetWifiEmptyStateMessage(WifiAccessIssue accessIssue)
        {
            return accessIssue switch
            {
                WifiAccessIssue.LocationPermissionRequired => "当前应用已获得内部权限，但 Windows 没有向 WLAN 查询开放定位能力。可以点击下方按钮打开定位权限设置。",
                WifiAccessIssue.ElevatedAccessRequired => "当前应用已获得内部权限，但系统要求以管理员身份访问附近 WiFi 列表。",
                _ => "当前无法读取附近 WiFi，已保存网络为空或系统限制了访问。"
            };
        }

        #endregion

        private void CloseAllPanels()
        {
            CancelHoverPopupClose();
            AppsPopup.IsOpen = false;
            WifiPopup.IsOpen = false;
            VolumePopup.IsOpen = false;
            OverflowAppsPopup.IsOpen = false;
            SetSystemIconHighlight(WifiIcon, false);
            SetSystemIconHighlight(VolumeIcon, false);
            SetSystemIconHighlight(AppsButton, false);
            SetSystemIconHighlight(OverflowFolderButton, false);
            UpdateRunningAppsRefreshInterval();
        }
    }
}
