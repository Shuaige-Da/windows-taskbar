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
using System.Runtime.InteropServices;
using Icon = System.Drawing.Icon;

namespace DynamicIslandBar
{
    public partial class MainWindow : Window
    {
        private const byte VirtualKeyMediaNextTrack = 0xB0;
        private const byte VirtualKeyMediaPreviousTrack = 0xB1;
        private const byte VirtualKeyMediaPlayPause = 0xB3;
        private const uint KeyEventKeyUp = 0x0002;
        private const double AutoHideOpacity = 0.1;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        private readonly DispatcherTimer _glowStopTimer;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _hoverCloseTimer;
        private readonly DispatcherTimer _capsuleHoverCollapseTimer;
        private readonly DispatcherTimer _autoHideTimer;
        private readonly DispatcherTimer _edgeRevealTimer;
        private readonly DispatcherTimer _appsRefreshTimer;
        private readonly DispatcherTimer _centerCardMediaRefreshTimer;
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _lyricsFastTimer;
        private readonly ICenterCardMediaSnapshotSource _centerCardMediaSource = new WindowsMediaSessionSnapshotSource();
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
        private bool _isCenterCardHovered;
        private Point _dragStartPoint;
        private double _dragStartLeft;
        private double _dragStartTop;
        private CapsuleSnapPreview? _activeSnapPreview;
        private double _floatingDragLeft;
        private double _floatingDragTop;
        private string? _lastPrimaryActivatedAppId;
        private DateTime _lastPrimaryActivatedAtUtc;
        private RunningAppEntry? _hoveredApp;
        private CenterCardMediaSnapshot? _centerCardLiveMediaSnapshot;
        private int _centerCardMediaRefreshVersion;
        private MediaService? _mediaService;
        private TimeSpan _mediaDuration = TimeSpan.Zero;
        private TimeSpan _lastMediaPosition = TimeSpan.Zero;
        private DateTime _lastMediaUpdateUtc = DateTime.MinValue;
        private bool _isProgressInterpolationActive;
        private bool _isMusicPlaying;
        private bool _isCapsuleAutoHidden;
        private int _volumeControlAppPid;
        private Slider? _appVolumeSlider;
        private Panel? _appVolumePanel;
        private LyricsService _lyricsService = new();
        private string? _lastDanmakuLyric;
        private int _nextDanmakuLaneIndex;
        private int _playbackModeIndex;
        private IReadOnlyList<LocalInstalledApp> _installedApps = [];
        private bool _installedAppsLoaded;

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
            _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            _edgeRevealTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _edgeRevealTimer.Tick += EdgeRevealTimer_Tick;
            _appsRefreshTimer = new DispatcherTimer { Interval = RunningAppsRefreshPolicy.GetInterval(false) };
            _appsRefreshTimer.Tick += AppsRefreshTimer_Tick;
            _centerCardMediaRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _centerCardMediaRefreshTimer.Tick += CenterCardMediaRefreshTimer_Tick;
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _progressTimer.Tick += ProgressTimer_Tick;
            _lyricsFastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _lyricsFastTimer.Tick += LyricsFastTimer_Tick;
            MouseMove += Window_MouseMove;
            MouseEnter += Window_MouseEnter;
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

        private readonly record struct NativePoint(int X, int Y);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PermissionService.Initialize(defaultAllowAll: true);
            _capsuleConfig = CapsuleConfigService.Load();
            _currentTheme = CapsuleThemeManager.BuildTheme(
                _capsuleConfig.ThemePreset,
                _capsuleConfig.BackgroundImagePath,
                _capsuleConfig.BackgroundImageOpacity);

            // Apply lyric language preference from config
            _lyricsService.PreferredLanguage = _capsuleConfig.LyricLanguage;

            InitGlowAnimation();
            InitDockAnimations();
            HideDemoDockItems();
            ApplyLayout();
            ApplyTheme();
            ApplyCenterCardWidth();
            StartCenterCardWaveAnimations();
            UpdateClock();
            UpdateBatteryStatus();
            RefreshRunningAppsBar();
            _appsRefreshTimer.Start();
            _centerCardMediaRefreshTimer.Start();
            _progressTimer.Start();
            _lyricsFastTimer.Start();
            _edgeRevealTimer.Start();
            ResetAutoHideTimer();
            _ = InitMediaServiceAsync();
            _windowLoaded = true;
            Dispatcher.BeginInvoke(MaybeWriteLayoutDiagnostics, DispatcherPriority.Loaded);
        }

        private async Task InitMediaServiceAsync()
        {
            try
            {
                _mediaService = new MediaService();
                await _mediaService.InitializeAsync();
            }
            catch { /* Media service unavailable */ }
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
                var (screenWidthDips, screenHeightDips) = GetPrimaryScreenSizeInDips();
                var dpi = VisualTreeHelper.GetDpi(this);
                builder.AppendLine($"screen={screenWidth}x{screenHeight} dips={screenWidthDips}x{screenHeightDips} dpi={dpi.DpiScaleX}x{dpi.DpiScaleY}");
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
            var (screenWidth, screenHeight) = GetPrimaryScreenSizeInDips();
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
            CapsuleGrid.Height = Height - 20;
            var capsuleHeight = CapsuleAppearanceMapper.MapCapsuleHeight(
                _currentLayoutMetrics.CapsuleHeight,
                _capsuleConfig.CapsuleThicknessPercent);
            CapsuleBorder.Width = _currentLayoutMetrics.CapsuleWidth;
            CapsuleBorder.Height = capsuleHeight;
            UpdateCapsuleCornerRadius(capsuleHeight);
            ApplyDockModeLayout();
            ApplyCenterCardWidth();
            CapsuleBorder.VerticalAlignment = _capsuleConfig.Mode == CapsuleMode.TopIsland
                ? VerticalAlignment.Top
                : VerticalAlignment.Center;
            CapsuleGrid.VerticalAlignment = _capsuleConfig.Mode switch
            {
                CapsuleMode.TopIsland => VerticalAlignment.Top,
                CapsuleMode.LeftDock or CapsuleMode.RightDock => VerticalAlignment.Center,
                _ => VerticalAlignment.Bottom
            };

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
            ConfigurePopup(CenterCardAppsPopup, _currentLayoutMetrics.PopupDirection, -120);
            ClearSnapPreview();
        }

        private void ApplyDockModeLayout()
        {
            var isSideDock = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;

            DockItems.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;
            AppIconsHost.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;
            SystemIconsHost.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;
            CenterCardSlot.Visibility = isSideDock ? Visibility.Collapsed : Visibility.Visible;
            DockItems.HorizontalAlignment = isSideDock ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            SystemIconsHost.HorizontalAlignment = isSideDock ? HorizontalAlignment.Center : HorizontalAlignment.Left;

            DockItems.Margin = isSideDock ? new Thickness(0, 12, 0, 12) : new Thickness(0, 0, 0, 0);
            AppIconsHost.Margin = isSideDock ? new Thickness(0, 6, 0, 6) : new Thickness(0, 0, 4, 0);
            OverflowFolderButton.Margin = isSideDock ? new Thickness(0, 2, 0, 0) : new Thickness(2, 0, 0, 0);

            CapsuleContentGrid.ColumnDefinitions.Clear();
            CapsuleContentGrid.RowDefinitions.Clear();

            if (isSideDock)
            {
                CapsuleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                CapsuleContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                CapsuleContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
                CapsuleContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetColumn(DockItems, 0);
                Grid.SetRow(DockItems, 0);
                Grid.SetColumn(CenterCardSlot, 0);
                Grid.SetRow(CenterCardSlot, 1);
                Grid.SetColumn(SystemIconsHost, 0);
                Grid.SetRow(SystemIconsHost, 2);
            }
            else
            {
                CapsuleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                CapsuleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                CapsuleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                CapsuleContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                CapsuleContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(DockItems, 0);
                Grid.SetRow(DockItems, 0);
                Grid.SetColumn(CenterCardSlot, 2);
                Grid.SetRow(CenterCardSlot, 0);
                Grid.SetColumn(SystemIconsHost, 3);
                Grid.SetRow(SystemIconsHost, 0);
            }

            foreach (var rootSeparator in CapsuleContentGrid.Children.OfType<Rectangle>())
            {
                rootSeparator.Visibility = isSideDock ? Visibility.Collapsed : Visibility.Visible;
            }

            UpdateSystemIconsLayout(isSideDock);
        }

        private void UpdateSystemIconsLayout(bool isSideDock)
        {
            foreach (var separator in SystemIconsHost.Children.OfType<Rectangle>())
            {
                separator.Width = isSideDock ? 30 : 1;
                separator.Height = isSideDock ? 1 : 30;
                separator.Margin = isSideDock ? new Thickness(0, 8, 0, 8) : new Thickness(8, 0, 8, 0);
            }
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

        private (double Width, double Height) GetPrimaryScreenSizeInDips()
        {
            return DisplayBoundsProvider.GetPrimaryScreenSize();
        }

        private static int MapVisibleAppSlots(CapsuleMode mode, double baseWidth, double capsuleWidth)
        {
            var maxSlots = mode == CapsuleMode.TopIsland ? 3 : 8;
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
            ApplyGlassPanelTheme(CenterCardAppsPanel);
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
            CapsuleBorder.BeginAnimation(HeightProperty, null);
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

        private async void CenterCardMediaRefreshTimer_Tick(object? sender, EventArgs e)
        {
            await RefreshCenterCardMediaSnapshotAsync();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            // Interpolate progress bar position between media refresh ticks for smooth movement
            if (!_isProgressInterpolationActive || !_isMusicPlaying || _mediaDuration.TotalSeconds <= 0)
                return;

            if (CenterCardProgressPanel.Visibility != Visibility.Visible)
                return;

            var elapsed = DateTime.UtcNow - _lastMediaUpdateUtc;
            var interpolatedPosition = _lastMediaPosition + elapsed;
            if (interpolatedPosition > _mediaDuration)
                interpolatedPosition = _mediaDuration;

            var pct = interpolatedPosition.TotalSeconds / _mediaDuration.TotalSeconds * 100;
            CenterCardProgressBar.Value = Math.Clamp(pct, 0, 100);
            CenterCardCurrentTime.Text = FormatTime(interpolatedPosition);
        }

        private void LyricsFastTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaService == null || !_isMusicPlaying || !_mediaService.HasSeenSongChange)
                return;

            var snapshot = _centerCardLiveMediaSnapshot;
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.Title))
                return;

            var position = _mediaService.GetCurrentPosition();
            if (position <= TimeSpan.Zero)
                return;

            var currentLyric = _lyricsService.GetCurrentLyric(position);
            if (!string.IsNullOrWhiteSpace(currentLyric) && currentLyric != snapshot.Lyric)
            {
                _centerCardLiveMediaSnapshot = snapshot with { Lyric = currentLyric };
                var app = GetPrimarySummaryApp();
                if (app != null)
                    UpdateActiveAppSummary(app, GetPrimarySummaryStatus(app));
            }
        }

        private async Task RefreshCenterCardMediaSnapshotAsync()
        {
            var app = GetPrimarySummaryApp();
            if (app == null)
            {
                _centerCardLiveMediaSnapshot = null;
                return;
            }

            var refreshVersion = ++_centerCardMediaRefreshVersion;
            var snapshot = await _centerCardMediaSource.TryGetSnapshotAsync(app);
            if (refreshVersion != _centerCardMediaRefreshVersion)
            {
                return;
            }

            // Preserve lyric from previous snapshot if same song
            var prevLyric = _centerCardLiveMediaSnapshot?.Lyric;
            if (!string.IsNullOrWhiteSpace(prevLyric)
                && snapshot != null
                && snapshot.Title == _centerCardLiveMediaSnapshot?.Title
                && snapshot.Artist == _centerCardLiveMediaSnapshot?.Artist)
            {
                snapshot = snapshot with { Lyric = prevLyric };
            }
            _centerCardLiveMediaSnapshot = snapshot;
            UpdateActiveAppSummary(app, GetPrimarySummaryStatus(app));

            if (snapshot == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaRefresh] No snapshot for '{app.DisplayName}' (AUMID={app.AppId})");
            }

            // Update progress bar from MediaService timeline
            if (_mediaService != null)
            {
                try
                {
                    // Tell MediaService which session to use (from the snapshot's AUMID)
                    var aumid = snapshot?.SourceAppUserModelId;
                    _mediaService.SetTargetSession(aumid);
                    System.Diagnostics.Debug.WriteLine($"[MediaRefresh] AUMID={aumid}, snapshot={(snapshot != null ? "ok" : "null")}");

                    var info = await _mediaService.GetMediaInfoAsync();
                    _isMusicPlaying = info.IsPlaying;
                    _mediaDuration = info.Duration;

                    System.Diagnostics.Debug.WriteLine($"[MediaRefresh] Title={info.Title}, Artist={info.Artist}, Playing={info.IsPlaying}, Pos={info.Position}, Dur={info.Duration}");

                    if (_isMusicPlaying && info.Duration.TotalSeconds > 0)
                    {
                        _lastMediaPosition = info.Position;
                        _lastMediaUpdateUtc = DateTime.UtcNow;
                        _isProgressInterpolationActive = true;

                        var pct = info.Position.TotalSeconds / info.Duration.TotalSeconds * 100;
                        CenterCardProgressBar.Value = Math.Clamp(pct, 0, 100);
                        CenterCardCurrentTime.Text = FormatTime(info.Position);
                        CenterCardTotalTime.Text = FormatTime(info.Duration);
                    }
                    else
                    {
                        _isProgressInterpolationActive = false;
                    }

                    // Update play/pause icon
                    CenterCardPlayPauseIcon.Data = Geometry.Parse(
                        _isMusicPlaying
                            ? "M6,4 L6,20 L10,20 L10,4 Z M14,4 L14,20 L18,20 L18,4 Z"
                            : "M8,5 L8,19 L19,12 Z");

                    // Fetch lyrics (slow: network call) - display update handled by LyricsFastTimer
                    if (snapshot != null && !string.IsNullOrWhiteSpace(info.Title))
                    {
                        await _lyricsService.EnsureLyricsAsync(
                            info.Title ?? string.Empty,
                            info.Artist ?? string.Empty,
                            info.Duration);

                        // Pass real duration from lyrics API to MediaService
                        if (_lyricsService.RealDuration > TimeSpan.Zero)
                            _mediaService.SetRealDuration(_lyricsService.RealDuration);
                    }
                    else
                    {
                        if (snapshot == null)
                            System.Diagnostics.Debug.WriteLine($"[MediaRefresh] Skipping lyrics: no snapshot");
                        else if (string.IsNullOrWhiteSpace(info.Title))
                            System.Diagnostics.Debug.WriteLine($"[MediaRefresh] Skipping lyrics: no title");
                        else if (info.Duration.TotalSeconds <= 0)
                            System.Diagnostics.Debug.WriteLine($"[MediaRefresh] Skipping lyrics: duration={info.Duration}");
                    }

                    // Sync playback mode from session
                    _playbackModeIndex = _mediaService.GetPlaybackMode();
                    System.Diagnostics.Debug.WriteLine($"[MediaRefresh] PlaybackMode={_playbackModeIndex}");
                    UpdatePlaybackModeIcon();

                    // Re-update progress panel visibility now that media state is fresh
                    var showProgress = _isCenterCardHovered && _isMusicPlaying && _mediaDuration.TotalSeconds > 0;
                    CenterCardProgressPanel.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
                    if (_isCenterCardHovered)
                    {
                        UpdateCenterCardHoverVisual(new CenterCardPresentation(
                            CenterCardDisplayMode.MusicDetails, string.Empty, string.Empty,
                            false, true, false));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MediaRefresh] Error: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private bool IsRunningAppsRefreshInteractive()
        {
            return _hoveredApp != null
                || CapsuleBorder.IsMouseOver
                || AppsPopup.IsOpen
                || OverflowAppsPopup.IsOpen
                || CenterCardAppsPopup.IsOpen;
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
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
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

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
        }

        private void AutoHideTimer_Tick(object? sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            if (!CapsuleAutoHidePolicy.CanHide(
                    _isDraggingCapsule,
                    CapsuleBorder.IsMouseOver || _isCenterCardHovered,
                    IsAnyPopupOpen()))
            {
                ResetAutoHideTimer();
                return;
            }

            FadeCapsuleTo(AutoHideOpacity);
            _isCapsuleAutoHidden = true;
        }

        private void EdgeRevealTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isCapsuleAutoHidden || !TryGetCursorScreenPoint(out var cursorPosition))
            {
                return;
            }

            var (screenWidth, screenHeight) = DisplayBoundsProvider.GetPrimaryScreenSize();
            if (!CapsuleAutoHidePolicy.IsPointerInRevealZone(
                    _capsuleConfig.Mode,
                    cursorPosition,
                    screenWidth,
                    screenHeight))
            {
                return;
            }

            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
        }

        private bool IsAnyPopupOpen()
        {
            return WifiPopup.IsOpen
                || VolumePopup.IsOpen
                || AppsPopup.IsOpen
                || OverflowAppsPopup.IsOpen
                || CenterCardAppsPopup.IsOpen
                || CenterCardVolumePopup.IsOpen
                || PermissionPromptPanel.Visibility == Visibility.Visible;
        }

        private void RestoreCapsuleVisibility()
        {
            if (!_isCapsuleAutoHidden && CapsuleGrid.Opacity >= 0.99)
            {
                return;
            }

            FadeCapsuleTo(1);
            _isCapsuleAutoHidden = false;
        }

        private void ResetAutoHideTimer()
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }

        private void FadeCapsuleTo(double targetOpacity)
        {
            var animation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            CapsuleGrid.BeginAnimation(OpacityProperty, animation);
        }

        private static bool TryGetCursorScreenPoint(out Point point)
        {
            point = default;
            if (!GetCursorPos(out var nativePoint))
            {
                return false;
            }

            point = new Point(nativePoint.X, nativePoint.Y);
            return true;
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

            if (CenterCardAppsPopup.IsOpen)
            {
                RenderCenterCardAppsPanel();
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
            var summaryApp = _hoveredApp ?? GetPrimarySummaryApp();
            UpdateActiveAppSummary(summaryApp, _hoveredApp == null ? GetPrimarySummaryStatus(summaryApp) : "正在运行");
        }

        private void RenderOverflowAppsPanel()
        {
            OverflowAppsListPanel.Children.Clear();

            var query = OverflowAppsSearchBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                EnsureInstalledAppsLoaded();
                var searchResults = LocalAppSearchService.Search(_installedApps, query)
                    .Take(24)
                    .ToList();

                foreach (var app in searchResults)
                {
                    OverflowAppsListPanel.Children.Add(CreateLocalAppSearchIcon(app, 44));
                }

                OverflowAppsEmptyStateText.Text = searchResults.Count == 0
                    ? "未找到可启动的本地应用"
                    : string.Empty;
                OverflowAppsEmptyStateText.Visibility = searchResults.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                return;
            }

            foreach (var app in _runningAppsSnapshot.OverflowApps)
            {
                OverflowAppsListPanel.Children.Add(CreateAppIcon(app, 44, expandOnHover: false));
            }

            OverflowAppsEmptyStateText.Text = _runningAppsSnapshot.OverflowApps.Count == 0
                ? "暂无更多运行中的应用"
                : string.Empty;
            OverflowAppsEmptyStateText.Visibility = _runningAppsSnapshot.OverflowApps.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void EnsureInstalledAppsLoaded()
        {
            if (_installedAppsLoaded)
            {
                return;
            }

            _installedApps = LocalAppSearchService.EnumerateInstalledApps();
            _installedAppsLoaded = true;
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

        private Border CreateLocalAppSearchIcon(LocalInstalledApp app, double size)
        {
            var border = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Margin = new Thickness(2, 0, 2, 6),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                ToolTip = app.DisplayName,
                Tag = app,
                ClipToBounds = true,
                Child = BuildAppIconVisual(app.DisplayName, app.LaunchPath, size * 0.48)
            };

            border.MouseEnter += (_, _) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(92, 76, 217, 100));
                RestoreCapsuleVisibility();
                ResetAutoHideTimer();
            };
            border.MouseLeave += (_, _) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
            };
            border.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                if (string.IsNullOrWhiteSpace(app.LaunchPath))
                {
                    return;
                }

                CloseAllPanels();
                HidePermissionPrompt();
                TryLaunchApp(app.LaunchPath);
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
            if (!string.IsNullOrWhiteSpace(_capsuleConfig.CenterCardAppId))
            {
                var selectedApp = _runningAppsSnapshot.AllApps.FirstOrDefault(app =>
                    app.IsRunning
                    && string.Equals(app.AppId, _capsuleConfig.CenterCardAppId, StringComparison.OrdinalIgnoreCase));
                if (selectedApp != null)
                {
                    return selectedApp;
                }
            }

            return _runningAppsSnapshot.MainBarApps.FirstOrDefault(app => app.IsForeground)
                ?? _runningAppsSnapshot.AllApps.FirstOrDefault(app => app.IsForeground)
                ?? _runningAppsSnapshot.MainBarApps.FirstOrDefault()
                ?? _runningAppsSnapshot.AllApps.FirstOrDefault(app => app.IsRunning);
        }

        private void UpdateActiveAppSummary(RunningAppEntry? app, string status)
        {
            UpdateCenterCardPresentation(app, status);
        }

        private void UpdateCenterCardPresentation(RunningAppEntry? app, string status)
        {
            if (app == null)
            {
                ActiveAppSummaryPanel.Visibility = Visibility.Collapsed;
                ActiveAppSummaryIcon.Content = null;
                CenterCardLyricsIcon.Content = null;
                ActiveAppSummaryTitle.Text = string.Empty;
                ActiveAppSummarySubtitle.Text = string.Empty;
                CenterCardLyricMarqueeText.Text = string.Empty;
                ClearCenterCardLyricsDanmaku();
                StopCenterCardLyricsMarquee();
                return;
            }

            var media = CenterCardMediaSnapshotProvider.Resolve(app, _centerCardLiveMediaSnapshot);
            var state = CenterCardPresentationPolicy.Build(app, status, media, _isCenterCardHovered);
            ActiveAppSummaryPanel.Visibility = Visibility.Visible;
            ActiveAppSummaryPanel.Tag = app;
            ActiveAppSummaryIcon.Content = BuildAppIconVisual(app, 20);
            CenterCardLyricsIcon.Content = BuildAppIconVisual(app, 20);
            ActiveAppSummaryTitle.Text = state.PrimaryText;
            ActiveAppSummarySubtitle.Text = state.SecondaryText;
            CenterCardLyricMarqueeText.Text = state.PrimaryText;
            CenterCardLyricsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;
            CenterCardDetailsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Collapsed : Visibility.Visible;
            CenterCardTransportControls.Visibility = state.ShowTransportControls ? Visibility.Visible : Visibility.Collapsed;

            // Show progress panel when music app + hovered + playing
            var showProgress = state.ShowTransportControls && _isMusicPlaying && _mediaDuration.TotalSeconds > 0;
            CenterCardProgressPanel.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;

            ApplyCenterCardTransportDensity();
            ApplyCenterCardLyricsLayout();
            UpdateCenterCardHoverVisual(state);

            if (state.ShowLyricsMarquee)
            {
                StartCenterCardLyricsMarquee();
            }
            else
            {
                StopCenterCardLyricsMarquee();
            }

            ApplyGlowAccent(app);
        }

        private void ApplyCenterCardWidth()
        {
            ActiveAppSummaryPanel.Width = MapCenterCardWidth(_capsuleConfig.CenterCardWidthPercent);
            ApplyCenterCardTransportDensity();
            ApplyCenterCardLyricsLayout();
        }

        private double MapCenterCardWidth(int percent)
        {
            var capsuleWidth = CapsuleBorder.Width > 0 ? CapsuleBorder.Width : _currentLayoutMetrics.CapsuleWidth;
            var availableSlotWidth = CenterCardSlot.ActualWidth > 0 ? CenterCardSlot.ActualWidth : 0;
            return CenterCardLayoutPolicy.MapWidth(_capsuleConfig.Mode, capsuleWidth, percent, availableSlotWidth);
        }

        private int MapCenterCardWidthPercent(double width)
        {
            var capsuleWidth = CapsuleBorder.Width > 0 ? CapsuleBorder.Width : _currentLayoutMetrics.CapsuleWidth;
            return CenterCardLayoutPolicy.MapWidthPercent(_capsuleConfig.Mode, capsuleWidth, width);
        }

        private void CenterCardSlot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyCenterCardWidth();
        }

        private void ApplyCenterCardTransportDensity()
        {
            var density = CenterCardLayoutPolicy.GetTransportDensity(ActiveAppSummaryPanel.Width);
            var showFullControls = density == CenterCardTransportDensity.Full;
            var showStepControls = density != CenterCardTransportDensity.Minimal;

            CenterCardPlaybackModeButton.Visibility = showFullControls ? Visibility.Visible : Visibility.Collapsed;
            CenterCardVolumeButton.Visibility = showFullControls ? Visibility.Visible : Visibility.Collapsed;
            CenterCardPreviousButton.Visibility = showStepControls ? Visibility.Visible : Visibility.Collapsed;
            CenterCardNextButton.Visibility = showStepControls ? Visibility.Visible : Visibility.Collapsed;
            CenterCardPlayPauseButton.Visibility = Visibility.Visible;
        }

        private void ApplyCenterCardLyricsLayout()
        {
            var layout = CenterCardLayoutPolicy.GetLyricsLayout(ActiveAppSummaryPanel.Width);

            CenterCardLyricsLayer.Margin = new Thickness(layout.HorizontalMargin, 0, layout.HorizontalMargin, 0);
            CenterCardLeftWave.Visibility = layout.ShowLeftWave ? Visibility.Visible : Visibility.Collapsed;
            CenterCardRightWave.Visibility = layout.ShowRightWave ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateCenterCardLyricsDanmaku(string? lyric)
        {
            var registration = CenterCardLyricsDanmakuPolicy.RegisterLyric(
                _lastDanmakuLyric,
                lyric,
                _nextDanmakuLaneIndex,
                laneCount: 3);
            _nextDanmakuLaneIndex = registration.NextLaneIndex;
            if (!registration.ShouldEnqueue || string.IsNullOrWhiteSpace(lyric))
            {
                return;
            }

            _lastDanmakuLyric = lyric;
            EnqueueCenterCardLyricsDanmaku(lyric, registration.LaneIndex);
        }

        private void ClearCenterCardLyricsDanmaku()
        {
            CenterCardLyricsDanmakuCanvas.Children.Clear();
            _lastDanmakuLyric = null;
            _nextDanmakuLaneIndex = 0;
        }

        private void EnqueueCenterCardLyricsDanmaku(string lyric, int laneIndex)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var viewportWidth = CenterCardLyricsViewport.ActualWidth > 0
                    ? CenterCardLyricsViewport.ActualWidth
                    : Math.Max(ActiveAppSummaryPanel.Width - 120, 180);
                if (viewportWidth <= 0)
                {
                    return;
                }

                var laneCount = 3;
                var laneHeight = Math.Max(18d, CenterCardLyricsViewport.ActualHeight > 0
                    ? CenterCardLyricsViewport.ActualHeight / laneCount
                    : 22d);
                var textBlock = new TextBlock
                {
                    Text = lyric,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Opacity = 0.94,
                    TextTrimming = TextTrimming.None
                };
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = Math.Max(textBlock.DesiredSize.Width, 120);
                Canvas.SetLeft(textBlock, viewportWidth + (laneIndex * 28));
                Canvas.SetTop(textBlock, laneIndex * laneHeight);
                CenterCardLyricsDanmakuCanvas.Children.Add(textBlock);

                var animation = new DoubleAnimation
                {
                    From = viewportWidth + (laneIndex * 28),
                    To = -(textWidth + 36),
                    Duration = TimeSpan.FromSeconds(Math.Clamp((viewportWidth + textWidth) / 92, 6, 12)),
                    BeginTime = TimeSpan.FromMilliseconds(laneIndex * 140),
                    FillBehavior = FillBehavior.Stop
                };
                animation.Completed += (_, _) => CenterCardLyricsDanmakuCanvas.Children.Remove(textBlock);
                textBlock.BeginAnimation(Canvas.LeftProperty, animation);
            }, DispatcherPriority.Loaded);
        }

        private void UpdateCenterCardHoverVisual(CenterCardPresentation state)
        {
            var expanded = _isCenterCardHovered;
            var showProgress = expanded && CenterCardProgressPanel.Visibility == Visibility.Visible;
            var targetHeight = expanded ? (showProgress ? 72 : 66) : 44;
            var targetRadius = targetHeight / 2;
            var duration = TimeSpan.FromMilliseconds(expanded ? 220 : 180);
            IEasingFunction easing = expanded
                ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 }
                : new QuadraticEase { EasingMode = EasingMode.EaseInOut };

            ActiveAppSummaryPanel.BeginAnimation(HeightProperty, new DoubleAnimation
            {
                To = targetHeight,
                Duration = duration,
                EasingFunction = easing
            });
            ActiveAppSummaryPanel.CornerRadius = new CornerRadius(targetRadius);
            CenterCardLeftResizeHandle.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = expanded ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(120)
            });
            CenterCardRightResizeHandle.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = expanded ? 1 : 0,
                Duration = TimeSpan.FromMilliseconds(120)
            });
        }

        private void StartCenterCardLyricsMarquee()
        {
            UpdateCenterCardLyricsDanmaku(CenterCardLyricMarqueeText.Text);
        }

        private void StopCenterCardLyricsMarquee()
        {
            ClearCenterCardLyricsDanmaku();
        }

        private void StartCenterCardWaveAnimations()
        {
            var bars = FindVisualChildren<Rectangle>(CenterCardLeftWave)
                .Concat(FindVisualChildren<Rectangle>(CenterCardRightWave))
                .ToList();
            for (var index = 0; index < bars.Count; index++)
            {
                var bar = bars[index];
                var targetHeight = 10 + ((index % 3) * 7);
                bar.BeginAnimation(HeightProperty, new DoubleAnimation
                {
                    From = Math.Max(6, targetHeight * 0.45),
                    To = targetHeight + 8,
                    Duration = TimeSpan.FromMilliseconds(520 + (index * 90)),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromMilliseconds(index * 80),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                });
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typed)
                {
                    yield return typed;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void CenterCard_MouseEnter(object sender, MouseEventArgs e)
        {
            _isCenterCardHovered = true;
            ExpandCapsuleForHover();
            UpdateActiveAppSummary(GetPrimarySummaryApp(), GetPrimarySummaryStatus(GetPrimarySummaryApp()));
        }

        private void CenterCard_MouseLeave(object sender, MouseEventArgs e)
        {
            _isCenterCardHovered = false;
            ScheduleCapsuleHoverCollapse();
            UpdateActiveAppSummary(GetPrimarySummaryApp(), GetPrimarySummaryStatus(GetPrimarySummaryApp()));
        }

        private void CenterCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ShouldSuppressCenterCardPrimaryAction(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (ActiveAppSummaryPanel.Tag is RunningAppEntry app)
            {
                e.Handled = true;
                HandleAppPrimaryAction(app);
            }
        }

        private static bool ShouldSuppressCenterCardPrimaryAction(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button or Thumb)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void CenterCardAppSelector_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenCenterCardAppsPopup();
        }

        private void CenterCardAppSelector_MouseEnter(object sender, MouseEventArgs e)
        {
            OpenCenterCardAppsPopup();
        }

        private void CenterCardAppSelector_MouseLeave(object sender, MouseEventArgs e)
        {
            ScheduleHoverPopupClose(GetCenterCardAppsPopupState());
        }

        private void CenterCardAppsPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            CancelHoverPopupClose();
        }

        private void CenterCardAppsPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            ScheduleHoverPopupClose(GetCenterCardAppsPopupState());
        }

        private void RefreshCenterCardAppsPanel()
        {
            RenderCenterCardAppsPanel();
        }

        private void OpenCenterCardAppsPopup()
        {
            RenderCenterCardAppsPanel();
            ShowHoverPopup(GetCenterCardAppsPopupState(), RefreshCenterCardAppsPanel);
        }

        private void RenderCenterCardAppsPanel()
        {
            CenterCardAppsListPanel.Children.Clear();
            var runningApps = _runningAppsSnapshot.AllApps
                .Where(app => app.IsRunning)
                .OrderByDescending(app => string.Equals(_capsuleConfig.CenterCardAppId, app.AppId, StringComparison.OrdinalIgnoreCase))
                .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (runningApps.Count == 0)
            {
                CenterCardAppsListPanel.Children.Add(new TextBlock
                {
                    Text = "暂无运行中应用",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    FontSize = 12
                });
                return;
            }

            foreach (var app in runningApps)
            {
                CenterCardAppsListPanel.Children.Add(CreateCenterCardAppPickerIcon(app));
            }
        }

        private Border CreateCenterCardAppPickerIcon(RunningAppEntry app)
        {
            var selected = string.Equals(_capsuleConfig.CenterCardAppId, app.AppId, StringComparison.OrdinalIgnoreCase);
            var accent = GetAppAccentColor(app);
            var icon = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(21),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(selected ? (byte)46 : (byte)24, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(selected ? (byte)160 : (byte)24, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(selected ? 1.4 : 1),
                ToolTip = app.DisplayName,
                Child = BuildAppIconVisual(app, 22)
            };
            icon.MouseLeftButtonDown += (_, args) =>
            {
                args.Handled = true;
                CapsuleConfigMutator.SetCenterCardApp(_capsuleConfig, app.AppId);
                CapsuleConfigService.Save(_capsuleConfig);
                _centerCardLiveMediaSnapshot = null;
                CenterCardAppsPopup.IsOpen = false;
                UpdateActiveAppSummary(GetPrimarySummaryApp(), GetPrimarySummaryStatus(GetPrimarySummaryApp()));
                _ = RefreshCenterCardMediaSnapshotAsync();
            };
            return icon;
        }

        private void CenterCardPrevious_Click(object sender, RoutedEventArgs e)
        {
            SendMediaKey(VirtualKeyMediaPreviousTrack);
        }

        private void CenterCardPlayPause_Click(object sender, RoutedEventArgs e)
        {
            SendMediaKey(VirtualKeyMediaPlayPause);
        }

        private void CenterCardNext_Click(object sender, RoutedEventArgs e)
        {
            SendMediaKey(VirtualKeyMediaNextTrack);
        }

        private async void CenterCardPlaybackMode_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaService == null)
                return;

            _playbackModeIndex = (_playbackModeIndex + 1) % 4;
            await _mediaService.SetPlaybackModeAsync(_playbackModeIndex);
            UpdatePlaybackModeIcon();
        }

        private void UpdatePlaybackModeIcon()
        {
            var tooltips = new[] { "顺序播放", "列表循环", "单曲循环", "随机播放" };
            var icons = new[]
            {
                // Sequential: three horizontal lines
                "M4,6 H20 M4,12 H20 M4,18 H14",
                // Loop All: circular arrows
                "M17,2 L21,6 L17,10 V7 H7 C5.9,7 5,7.9 5,9 M7,22 L3,18 L7,14 V17 H17 C18.1,17 19,16.1 19,15",
                // Loop One: circular arrow with center dot
                "M17,2 L21,6 L17,10 V7 H7 C5.9,7 5,7.9 5,9 M7,22 L3,18 L7,14 V17 H17 C18.1,17 19,16.1 19,15 M12,11 V13",
                // Shuffle: crossed arrows
                "M16,3 L20,3 L20,7 M20,3 L4,21 M14,21 L20,21 L20,15 M4,3 L10,3 L4,9"
            };

            CenterCardPlaybackModeIcon.Data = Geometry.Parse(icons[_playbackModeIndex]);
            CenterCardPlaybackModeButton.ToolTip = tooltips[_playbackModeIndex];
        }

        private void CenterCardVolume_Click(object sender, RoutedEventArgs e)
        {
            _volumeControlAppPid = _mediaService?.GetMusicAppProcessId() ?? 0;
            System.Diagnostics.Debug.WriteLine($"[Volume] CenterCardVolume_Click: PID={_volumeControlAppPid}");
            UpdateCenterCardVolumeSlider();
            CenterCardVolumePopup.IsOpen = !CenterCardVolumePopup.IsOpen;
        }

        private void UpdateCenterCardVolumeSlider()
        {
            int vol;
            if (_volumeControlAppPid > 0)
            {
                vol = AudioService.GetAppVolume(_volumeControlAppPid);
                if (vol < 0)
                    vol = AudioService.GetVolume(); // Fallback to system volume
            }
            else
            {
                vol = AudioService.GetVolume();
            }

            _suppressVolumeEvent = true;
            CenterCardVolumeSlider.Value = vol >= 0 ? vol : 0;
            CenterCardVolumePercentText.Text = vol >= 0 ? $"{vol}%" : "N/A";
            _suppressVolumeEvent = false;
        }

        private void CenterCardVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressVolumeEvent || !_windowLoaded)
                return;

            var pct = (int)CenterCardVolumeSlider.Value;
            CenterCardVolumePercentText.Text = $"{pct}%";

            if (_volumeControlAppPid > 0)
            {
                if (!AudioService.SetAppVolume(_volumeControlAppPid, pct))
                    AudioService.SetVolume(pct); // Fallback
            }
            else
            {
                AudioService.SetVolume(pct);
            }
        }

        private void CenterCardVolumePopup_MouseEnter(object sender, MouseEventArgs e)
        {
            // Keep popup open while mouse is over it
        }

        private void CenterCardVolumePopup_MouseLeave(object sender, MouseEventArgs e)
        {
            CenterCardVolumePopup.IsOpen = false;
        }

        private static void SendMediaKey(byte virtualKey)
        {
            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKey, 0, KeyEventKeyUp, UIntPtr.Zero);
        }

        private async void CenterCardProgress_Click(object sender, MouseButtonEventArgs e)
        {
            if (_mediaService == null || _mediaDuration.TotalMilliseconds <= 0)
            {
                return;
            }

            var progressBar = CenterCardProgressBar;
            var clickPosition = e.GetPosition(progressBar).X;
            var ratio = Math.Clamp(clickPosition / progressBar.ActualWidth, 0, 1);
            var targetMs = (long)(ratio * _mediaDuration.TotalMilliseconds);

            try
            {
                await _mediaService.SeekAsync(targetMs);
                CenterCardProgressBar.Value = ratio * 100;
                CenterCardCurrentTime.Text = FormatTime(TimeSpan.FromMilliseconds(targetMs));
            }
            catch { /* Ignore seek errors */ }
        }

        private static string FormatTime(TimeSpan time)
        {
            var totalMinutes = (int)time.TotalMinutes;
            var seconds = time.Seconds;
            return $"{totalMinutes}:{seconds:D2}";
        }

        private void CenterCardResizeHandle_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var side = sender is FrameworkElement element ? element.Tag?.ToString() : "Right";
            var delta = string.Equals(side, "Left", StringComparison.OrdinalIgnoreCase)
                ? -e.HorizontalChange * 2
                : e.HorizontalChange * 2;
            var targetWidth = ActiveAppSummaryPanel.Width + delta;
            CapsuleConfigMutator.SetCenterCardWidthPercent(_capsuleConfig, MapCenterCardWidthPercent(targetWidth));
            ApplyCenterCardWidth();
        }

        private void CenterCardResizeHandle_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            CapsuleConfigService.Save(_capsuleConfig);
        }

        private string GetPrimarySummaryStatus(RunningAppEntry? app)
        {
            if (app == null)
            {
                return "当前窗口";
            }

            if (!string.IsNullOrWhiteSpace(_capsuleConfig.CenterCardAppId)
                && string.Equals(app.AppId, _capsuleConfig.CenterCardAppId, StringComparison.OrdinalIgnoreCase))
            {
                return "中心应用";
            }

            return app.IsForeground ? "当前窗口" : "正在运行";
        }

        private FrameworkElement BuildAppIconVisual(RunningAppEntry app, double iconSize)
            => BuildAppIconVisual(app.DisplayName, app.ExePath, iconSize);

        private FrameworkElement BuildAppIconVisual(string displayName, string? exePath, double iconSize)
        {
            var iconSource = GetIconSource(exePath);
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
                Text = GetFallbackIconGlyph(displayName),
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

            var centerCardItem = new MenuItem
            {
                Header = string.Equals(_capsuleConfig.CenterCardAppId, app.AppId, StringComparison.OrdinalIgnoreCase)
                    ? "取消中心卡片"
                    : "设为中心卡片",
                Foreground = Brushes.White,
                IsEnabled = app.IsRunning
            };
            centerCardItem.Click += (_, _) =>
            {
                var nextAppId = string.Equals(_capsuleConfig.CenterCardAppId, app.AppId, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : app.AppId;
                CapsuleConfigMutator.SetCenterCardApp(_capsuleConfig, nextAppId);
                CapsuleConfigService.Save(_capsuleConfig);
                _centerCardLiveMediaSnapshot = null;
                UpdateActiveAppSummary(GetPrimarySummaryApp(), GetPrimarySummaryStatus(GetPrimarySummaryApp()));
                _ = RefreshCenterCardMediaSnapshotAsync();
            };

            menu.Items.Add(visibilityItem);
            menu.Items.Add(runItem);
            menu.Items.Add(favoriteItem);
            menu.Items.Add(centerCardItem);

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

            // Lyric language submenu
            var lyricMenu = new MenuItem { Header = "歌词语言", Foreground = Brushes.White };
            var simplifiedItem = new MenuItem
            {
                Header = "简体中文",
                Foreground = Brushes.White,
                IsCheckable = true,
                IsChecked = _capsuleConfig.LyricLanguage == LyricLanguage.Simplified
            };
            simplifiedItem.Click += (_, _) =>
            {
                CapsuleConfigMutator.SetLyricLanguage(_capsuleConfig, LyricLanguage.Simplified);
                CapsuleConfigService.Save(_capsuleConfig);
                _lyricsService.PreferredLanguage = LyricLanguage.Simplified;
                UpdateActiveAppSummary(GetPrimarySummaryApp(), GetPrimarySummaryStatus(GetPrimarySummaryApp()));
            };

            var traditionalItem = new MenuItem
            {
                Header = "繁體中文",
                Foreground = Brushes.White,
                IsCheckable = true,
                IsChecked = _capsuleConfig.LyricLanguage == LyricLanguage.Traditional
            };
            traditionalItem.Click += (_, _) =>
            {
                CapsuleConfigMutator.SetLyricLanguage(_capsuleConfig, LyricLanguage.Traditional);
                CapsuleConfigService.Save(_capsuleConfig);
                _lyricsService.PreferredLanguage = LyricLanguage.Traditional;
                UpdateActiveAppSummary(GetPrimarySummaryApp(), GetPrimarySummaryStatus(GetPrimarySummaryApp()));
            };

            lyricMenu.Items.Add(simplifiedItem);
            lyricMenu.Items.Add(traditionalItem);
            settingsMenu.Items.Add(lyricMenu);

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
            if (refreshLayout)
            {
                ApplyLayout();
                RefreshRunningAppsBar();
            }
            else
            {
                ApplyTheme();
            }

            CapsuleConfigService.Save(_capsuleConfig);
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
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
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
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
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
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
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
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();
            CancelHoverPopupClose();
            SetSystemIconHighlight(OverflowFolderButton, true);
        }

        private void OverflowAppsPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(OverflowFolderButton, false);
            ScheduleHoverPopupClose(GetOverflowPopupState());
        }

        private void OverflowAppsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RestoreCapsuleVisibility();
            ResetAutoHideTimer();

            if (OverflowAppsPopup.IsOpen)
            {
                RenderOverflowAppsPanel();
            }
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

        private void ClearCenterCardAppSelectorHighlight()
        {
            CenterCardAppSelectorButton.Background = Brushes.Transparent;
            CenterCardAppSelectorButton.BorderBrush = Brushes.Transparent;
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

        private PopupState GetCenterCardAppsPopupState() => new()
        {
            Icon = CenterCardAppSelectorButton,
            Panel = CenterCardAppsPanel,
            Popup = CenterCardAppsPopup
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
            ClearCenterCardAppSelectorHighlight();

            if (ReferenceEquals(sender, OverflowAppsPopup)
                && !string.IsNullOrEmpty(OverflowAppsSearchBox.Text))
            {
                OverflowAppsSearchBox.Text = string.Empty;
            }

            // Clean up dynamic app volume panel
            if (_appVolumePanel != null)
            {
                (VolumePanel.Child as Panel)?.Children.Remove(_appVolumePanel);
                _appVolumePanel = null;
                _appVolumeSlider = null;
            }
            _volumeControlAppPid = 0;

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
            CaptureFloatingPosition();
            ClearSnapPreview();
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
            UpdateSnapPreview(currentPoint);
            CaptureFloatingPosition();
        }

        private void Capsule_DragEnd(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingCapsule)
            {
                return;
            }

            _isDraggingCapsule = false;
            Mouse.Capture(null);
            CaptureFloatingPosition();
            ClearSnapPreview();
            e.Handled = true;

            var resolvedMode = CapsuleLayoutManager.ResolveDropMode(
                GetPrimaryScreenSizeInDips().Width,
                GetPrimaryScreenSizeInDips().Height,
                Left,
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

        private void UpdateSnapPreview(Point cursorScreenPoint)
        {
            var (screenWidth, screenHeight) = GetPrimaryScreenSizeInDips();
            var previewEdge = ResolvePreviewEdge(cursorScreenPoint, screenWidth, screenHeight);
            if (previewEdge == SnapEdge.None)
            {
                ClearSnapPreview();
                return;
            }

            var topMetrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.TopIsland, screenWidth, screenHeight);
            var bottomMetrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, screenWidth, screenHeight);
            var topCapsuleWidth = CapsuleAppearanceMapper.MapCapsuleWidth(
                CapsuleMode.TopIsland,
                topMetrics.CapsuleWidth,
                _capsuleConfig.CapsuleLengthPercent);
            var topCapsuleHeight = CapsuleAppearanceMapper.MapCapsuleHeight(
                topMetrics.CapsuleHeight,
                _capsuleConfig.CapsuleThicknessPercent);
            var bottomCapsuleWidth = CapsuleAppearanceMapper.MapCapsuleWidth(
                CapsuleMode.BottomTaskbar,
                bottomMetrics.CapsuleWidth,
                _capsuleConfig.CapsuleLengthPercent);
            var bottomCapsuleHeight = CapsuleAppearanceMapper.MapCapsuleHeight(
                bottomMetrics.CapsuleHeight,
                _capsuleConfig.CapsuleThicknessPercent);

            var preview = CapsuleLayoutManager.BuildSnapPreview(
                previewEdge,
                screenWidth,
                screenHeight,
                topCapsuleWidth,
                topCapsuleHeight,
                bottomCapsuleWidth,
                bottomCapsuleHeight);

            ApplySnapPreview(preview);
        }

        private void ApplySnapPreview(CapsuleSnapPreview preview)
        {
            _activeSnapPreview = preview;
            CapsuleSnapPreviewOutline.Width = preview.CapsuleWidth;
            CapsuleSnapPreviewOutline.Height = preview.CapsuleHeight;
            CapsuleSnapPreviewOutline.CornerRadius = new CornerRadius(preview.CapsuleHeight / 2);
            var screenOrigin = CapsuleSnapPreviewGeometry.ComputeOutlineOrigin(
                preview.Frame,
                preview.CapsuleWidth,
                preview.CapsuleHeight,
                preview.RotationDegrees);
            var previewOrigin = CapsuleGrid.PointFromScreen(screenOrigin);
            Canvas.SetLeft(CapsuleSnapPreviewOutline, previewOrigin.X);
            Canvas.SetTop(CapsuleSnapPreviewOutline, previewOrigin.Y);
            CapsuleSnapPreviewOutline.RenderTransformOrigin = new Point(0.5, 0.5);
            CapsuleSnapPreviewOutline.RenderTransform = preview.RotationDegrees == 0
                ? Transform.Identity
                : new RotateTransform(preview.RotationDegrees);
            CapsuleSnapPreviewOutline.Visibility = Visibility.Visible;
        }

        private void ClearSnapPreview()
        {
            _activeSnapPreview = null;
            CapsuleSnapPreviewOutline.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(CapsuleSnapPreviewOutline, 0);
            Canvas.SetTop(CapsuleSnapPreviewOutline, 0);
            CapsuleSnapPreviewOutline.RenderTransform = Transform.Identity;
        }

        private void CaptureFloatingPosition()
        {
            if (_activeSnapPreview != null)
            {
                return;
            }

            _floatingDragLeft = Left;
            _floatingDragTop = Top;
        }

        private static SnapEdge ResolvePreviewEdge(Point cursorScreenPoint, double screenWidth, double screenHeight)
        {
            const double snapPreviewThreshold = 72;

            if (cursorScreenPoint.X <= snapPreviewThreshold)
            {
                return SnapEdge.Left;
            }

            if (cursorScreenPoint.X >= screenWidth - snapPreviewThreshold)
            {
                return SnapEdge.Right;
            }

            if (cursorScreenPoint.Y <= snapPreviewThreshold)
            {
                return SnapEdge.Top;
            }

            if (cursorScreenPoint.Y >= screenHeight - snapPreviewThreshold)
            {
                return SnapEdge.Bottom;
            }

            return SnapEdge.None;
        }

        private bool ShouldSuppressDragStart(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button or Slider or Thumb)
                {
                    return true;
                }

                if (source is FrameworkElement element)
                {
                    if (ReferenceEquals(element, ActiveAppSummaryPanel)
                        || ReferenceEquals(element, CenterCardLeftResizeHandle)
                        || ReferenceEquals(element, CenterCardRightResizeHandle))
                    {
                        return true;
                    }

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

            // App volume section: show music app volume slider when triggered from center card
            var mainStack = VolumePanel.Child as Panel;
            if (mainStack != null)
            {
                // Remove previously added app volume elements
                if (_appVolumePanel != null)
                {
                    mainStack.Children.Remove(_appVolumePanel);
                    _appVolumePanel = null;
                    _appVolumeSlider = null;
                }

                if (_volumeControlAppPid > 0)
                {
                    var appPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                    appPanel.Children.Add(new TextBlock
                    {
                        Text = "音乐音量",
                        Foreground = new SolidColorBrush(Color.FromArgb(200, 76, 217, 100)),
                        FontSize = 12, FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 6)
                    });

                    var dock = new DockPanel { Margin = new Thickness(0, 0, 0, 0) };
                    dock.Children.Add(new TextBlock
                    {
                        Text = "\uD83C\uDFB5",
                        Foreground = Brushes.White, FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    });

                    var appPctText = new TextBlock
                    {
                        Text = "--%",
                        Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                        FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    DockPanel.SetDock(appPctText, Dock.Right);
                    dock.Children.Add(appPctText);

                    var appSlider = new Slider
                    {
                        Minimum = 0, Maximum = 100,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 93, 187)),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var appVol = AudioService.GetAppVolume(_volumeControlAppPid);
                    if (appVol >= 0)
                    {
                        appSlider.Value = appVol;
                        appPctText.Text = $"{appVol}%";
                    }
                    else
                    {
                        appPctText.Text = "N/A";
                    }

                    appSlider.ValueChanged += AppVolumeSlider_ValueChanged;
                    dock.Children.Add(appSlider);
                    appPanel.Children.Add(dock);

                    // Separator
                    appPanel.Children.Add(new Border
                    {
                        Height = 1, Margin = new Thickness(0, 6, 0, 8),
                        Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                    });

                    // Insert after title (index 1)
                    if (mainStack.Children.Count > 1)
                        mainStack.Children.Insert(1, appPanel);
                    else
                        mainStack.Children.Add(appPanel);

                    _appVolumePanel = appPanel;
                    _appVolumeSlider = appSlider;
                }
            }

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

        private void AppVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressVolumeEvent || !_windowLoaded || _volumeControlAppPid <= 0)
            {
                return;
            }

            var pct = (int)((Slider)sender).Value;
            AudioService.SetAppVolume(_volumeControlAppPid, pct);
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
            CenterCardAppsPopup.IsOpen = false;
            SetSystemIconHighlight(WifiIcon, false);
            SetSystemIconHighlight(VolumeIcon, false);
            SetSystemIconHighlight(AppsButton, false);
            SetSystemIconHighlight(OverflowFolderButton, false);
            ClearCenterCardAppSelectorHighlight();
            UpdateRunningAppsRefreshInterval();
        }
    }

    public static class CapsuleSnapPreviewGeometry
    {
        public static Point ComputeOutlineOrigin(
            WindowFrame frame,
            double capsuleWidth,
            double capsuleHeight,
            double rotationDegrees)
        {
            var renderedBounds = ComputeRenderedBounds(
                new Point(0, 0),
                capsuleWidth,
                capsuleHeight,
                rotationDegrees);
            var renderedLeft = frame.Left + ((frame.Width - renderedBounds.Width) / 2);
            var renderedTop = frame.Top + ((frame.Height - renderedBounds.Height) / 2);

            if (IsQuarterTurn(rotationDegrees))
            {
                var halfDelta = (capsuleWidth - capsuleHeight) / 2;
                return new Point(
                    renderedLeft - halfDelta,
                    renderedTop + halfDelta);
            }

            return new Point(renderedLeft, renderedTop);
        }

        public static Rect ComputeRenderedBounds(
            Point origin,
            double capsuleWidth,
            double capsuleHeight,
            double rotationDegrees)
        {
            if (IsQuarterTurn(rotationDegrees))
            {
                var halfDelta = (capsuleWidth - capsuleHeight) / 2;
                return new Rect(
                    origin.X + halfDelta,
                    origin.Y - halfDelta,
                    capsuleHeight,
                    capsuleWidth);
            }

            return new Rect(origin.X, origin.Y, capsuleWidth, capsuleHeight);
        }

        private static bool IsQuarterTurn(double rotationDegrees)
        {
            var normalized = ((rotationDegrees % 180) + 180) % 180;
            return Math.Abs(normalized - 90) < 0.001;
        }
    }
}
