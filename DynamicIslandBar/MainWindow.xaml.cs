using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DynamicIslandBar
{
    public partial class MainWindow : Window
    {


        private readonly DispatcherTimer _glowStopTimer;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _hoverCloseTimer;
        private Storyboard? _glowSpinStoryboard;
        private Storyboard? _dockExpandStoryboard;
        private Storyboard? _dockCollapseStoryboard;
        private int _hoveredCount = 0;
        private bool _suppressVolumeEvent = false;
        private bool _windowLoaded = false;
        private int _wifiRefreshVersion = 0;
        private int _volumeRefreshVersion = 0;
        private PopupState? _pendingHoverClosePopup;
        private PermissionPromptState? _pendingPermissionPrompt;
        private WifiAccessIssue _lastWifiAccessIssue = WifiAccessIssue.None;

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
        }

        private sealed class PopupState
        {
            public required Border Icon { get; init; }
            public required Border Panel { get; init; }
            public required System.Windows.Controls.Primitives.Popup Popup { get; init; }
        }

        private sealed class PermissionPromptState
        {
            public required AppPermission Permission { get; init; }
            public required Action GrantedAction { get; init; }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PermissionService.Initialize(defaultAllowAll: true);
            PositionWindow();
            InitGlowAnimation();
            InitDockAnimations();
            UpdateClock();
            UpdateBatteryStatus();
            TaskbarManager.Show();
            _windowLoaded = true;
        }

        private void PositionWindow()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = screenHeight - this.Height;
        }

        #region ClockAndBattery

        private void ClockTimer_Tick(object? sender, EventArgs e) { UpdateClock(); }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            ClockText.Text = now.ToString("HH:mm");
            DateText.Text = now.ToString("M\u6708d\u65e5 ddd");
        }

        private void UpdateBatteryStatus()
        {
            var pct = SystemInfoService.GetBatteryPercent();
            if (pct >= 0)
            {
                BatteryPercentText.Text = pct == 100 ? "" : $"{pct}%";
                BatteryIcon.ToolTip = SystemInfoService.GetBatteryInfo();
            }
        }

        #endregion

        #region GlowAnimation

        private void InitGlowAnimation()
        {
            _glowSpinStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var dashAnim = new DoubleAnimation
            {
                From = 0, To = -285, Duration = TimeSpan.FromSeconds(2)
            };
            Storyboard.SetTarget(dashAnim, GlowPath);
            Storyboard.SetTargetProperty(dashAnim, new PropertyPath(Shape.StrokeDashOffsetProperty));
            _glowSpinStoryboard.Children.Add(dashAnim);
        }

        private void InitDockAnimations()
        {
            _dockExpandStoryboard = new Storyboard();
            var heightAnim = new DoubleAnimation
            {
                To = 76, Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.8 }
            };
            Storyboard.SetTarget(heightAnim, CapsuleBorder);
            Storyboard.SetTargetProperty(heightAnim, new PropertyPath(Border.HeightProperty));
            _dockExpandStoryboard.Children.Add(heightAnim);

            _dockCollapseStoryboard = new Storyboard();
            var heightAnimBack = new DoubleAnimation
            {
                To = 64, Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseInOut, Amplitude = 0.4 }
            };
            Storyboard.SetTarget(heightAnimBack, CapsuleBorder);
            Storyboard.SetTargetProperty(heightAnimBack, new PropertyPath(Border.HeightProperty));
            _dockCollapseStoryboard.Children.Add(heightAnimBack);
        }

        private void DockItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is not Border item) return;
            if (item.Tag is string colorStr)
            {
                var color = (Color)ColorConverter.ConvertFromString(colorStr);
                GlowPath.Stroke = new SolidColorBrush(color);
            }
            _glowStopTimer.Stop();
            _hoveredCount++;
            ExpandItem(item);
            _dockExpandStoryboard?.Begin();
            GlowPath.BeginAnimation(UIElement.OpacityProperty, null);
            GlowPath.Opacity = 1;
            _glowSpinStoryboard?.Begin();
        }

        private void DockItem_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is not Border item) return;
            CollapseItem(item);
            _hoveredCount--;
            _glowStopTimer.Start();
        }

        private void GlowStopTimer_Tick(object? sender, EventArgs e)
        {
            _glowStopTimer.Stop();
            // Check if mouse is still over any dock item
            bool stillHovering = ItemMusic.IsMouseOver || ItemPhone.IsMouseOver || ItemNav.IsMouseOver;
            if (stillHovering) return;

            _hoveredCount = 0;
            _glowSpinStoryboard?.Stop();
            var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400) };
            GlowPath.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            _dockCollapseStoryboard?.Begin();
        }

        private void ExpandItem(Border item)
        {
            var expandedPanel = FindExpandedPanel(item);
            if (expandedPanel != null) expandedPanel.Visibility = Visibility.Visible;
            var anim = new DoubleAnimation
            {
                To = 220, Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }
            };
            item.BeginAnimation(Border.MaxWidthProperty, anim);
            item.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
        }

        private void CollapseItem(Border item)
        {
            var expandedPanel = FindExpandedPanel(item);
            if (expandedPanel != null) expandedPanel.Visibility = Visibility.Collapsed;
            var anim = new DoubleAnimation
            {
                To = 48, Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseInOut, Amplitude = 0.4 }
            };
            item.BeginAnimation(Border.MaxWidthProperty, anim);
            item.Background = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255));
        }

        private StackPanel? FindExpandedPanel(Border item)
        {
            if (item.Child is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is StackPanel inner && inner.Name.Contains("Expanded"))
                        return inner;
                }
            }
            return null;
        }

        #endregion

        #region AppsList

        private void AppsButton_Click(object sender, MouseButtonEventArgs e)
        {
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenTaskManager();
        }

        private void RefreshAppsList()
        {
            RequestPermission(
                AppPermission.RunningApps,
                "后台列表权限",
                "允许读取当前运行中的窗口列表，用于显示方格按钮里的后台程序面板。",
                RefreshAppsListCore);
        }

        private void RefreshAppsListCore()
        {
            AppsListPanel.Children.Clear();
            var windows = WindowManager.GetVisibleWindows();
            if (windows.Count == 0)
            {
                AppsListPanel.Children.Add(new TextBlock
                {
                    Text = "\u6682\u65e0\u8fd0\u884c\u4e2d\u7684\u7a97\u53e3",
                    Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                });
                return;
            }
            foreach (var win in windows)
            {
                var btn = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                    Child = new TextBlock
                    {
                        Text = win.Title.Length > 30 ? win.Title[..27] + "..." : win.Title,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                        FontSize = 13,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                };
                btn.MouseEnter += (s, args) => btn.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                btn.MouseLeave += (s, args) => btn.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                btn.MouseLeftButtonDown += (s, args) =>
                {
                    win.Activate();
                    CloseAllPanels();
                };
                AppsListPanel.Children.Add(btn);
            }
        }

        #endregion

        #region ContextMenu

        private void Capsule_RightClick(object sender, MouseButtonEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            menu.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            menu.Foreground = System.Windows.Media.Brushes.White;

            var exitItem = new System.Windows.Controls.MenuItem { Header = "\u9000\u51fa\u7a0b\u5e8f", Foreground = System.Windows.Media.Brushes.White };
            exitItem.Click += (s, args) => { TaskbarManager.Show(); System.Windows.Application.Current.Shutdown(); };

            var hideTaskbar = new System.Windows.Controls.MenuItem { Header = "\u9690\u85cf\u7cfb\u7edf\u4efb\u52a1\u680f", Foreground = System.Windows.Media.Brushes.White };
            hideTaskbar.Click += (s, args) => TaskbarManager.Hide();

            var showTaskbar = new System.Windows.Controls.MenuItem { Header = "\u663e\u793a\u7cfb\u7edf\u4efb\u52a1\u680f", Foreground = System.Windows.Media.Brushes.White };
            showTaskbar.Click += (s, args) => TaskbarManager.Show();

            menu.Items.Add(hideTaskbar);
            menu.Items.Add(showTaskbar);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            if (sender is Border border)
            {
                border.ContextMenu = menu;
                menu.IsOpen = true;
            }
        }

        #endregion

        #region SystemIcons

        private void SystemIcon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                SetSystemIconHighlight(border, true);
                if (border.Name == "BatteryIcon")
                {
                    border.ToolTip = SystemInfoService.GetBatteryInfo();
                }
            }
        }

        private void SystemIcon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                SetSystemIconHighlight(border, false);
            }
        }

        private void WifiIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(WifiIcon, true);
            ShowHoverPopup(GetWifiPopupState(), RefreshWifiPanel);
        }

        private void WifiIcon_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(WifiIcon, false);
            ScheduleHoverPopupClose(GetWifiPopupState());
        }

        private void VolumeIcon_MouseEnter(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(VolumeIcon, true);
            ShowHoverPopup(GetVolumePopupState(), RefreshVolumePanel);
        }

        private void VolumeIcon_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(VolumeIcon, false);
            ScheduleHoverPopupClose(GetVolumePopupState());
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

        private void SetSystemIconHighlight(Border border, bool highlighted)
        {
            border.Background = new SolidColorBrush(
                highlighted
                    ? Color.FromArgb(30, 255, 255, 255)
                    : Color.FromArgb(0, 255, 255, 255));
        }

        private PopupState GetWifiPopupState() =>
            new()
            {
                Icon = WifiIcon,
                Panel = WifiPanel,
                Popup = WifiPopup
            };

        private PopupState GetVolumePopupState() =>
            new()
            {
                Icon = VolumeIcon,
                Panel = VolumePanel,
                Popup = VolumePopup
            };

        private PopupState GetAppsPopupState() =>
            new()
            {
                Icon = AppsButton,
                Panel = AppsPanel,
                Popup = AppsPopup
            };

        private void ShowHoverPopup(PopupState popupState, Action refreshAction)
        {
            CancelHoverPopupClose();
            if (!popupState.Popup.IsOpen)
            {
                CloseAllPanels();
                popupState.Popup.IsOpen = true;
            }

            refreshAction();
        }

        private void AppsButton_MouseEnter(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(AppsButton, true);
            ShowHoverPopup(GetAppsPopupState(), RefreshAppsList);
        }

        private void AppsButton_MouseLeave(object sender, MouseEventArgs e)
        {
            SetSystemIconHighlight(AppsButton, false);
            ScheduleHoverPopupClose(GetAppsPopupState());
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
        }

        private void WifiIcon_Click(object sender, MouseButtonEventArgs e)
        {
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenWifiSettings();
        }

        private void VolumeIcon_Click(object sender, MouseButtonEventArgs e)
        {
            CloseAllPanels();
            HidePermissionPrompt();
            SystemInfoService.OpenSoundSettings();
        }

        private void BatteryIcon_Click(object sender, MouseButtonEventArgs e)
        {
            HidePermissionPrompt();
            SystemInfoService.OpenBatterySettings();
        }

        private void Popup_Closed(object sender, EventArgs e)
        {
            CancelHoverPopupClose();
            SetSystemIconHighlight(WifiIcon, WifiIcon.IsMouseOver || WifiPanel.IsMouseOver);
            SetSystemIconHighlight(VolumeIcon, VolumeIcon.IsMouseOver || VolumePanel.IsMouseOver);
            SetSystemIconHighlight(AppsButton, AppsButton.IsMouseOver || AppsPanel.IsMouseOver);
            _wifiRefreshVersion++;
            _volumeRefreshVersion++;
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
                            Cursor = System.Windows.Input.Cursors.Hand,
                            Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255))
                        };
                        var panel = new DockPanel();
                        var nameBlock = new TextBlock
                        {
                            Text = net.Ssid,
                            Foreground = net.IsConnected ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                            FontSize = 13, VerticalAlignment = System.Windows.VerticalAlignment.Center
                        };
                        var signalBlock = new TextBlock
                        {
                            Text = net.SignalStrength,
                            Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                            FontSize = 11, VerticalAlignment = System.Windows.VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        panel.Children.Add(nameBlock);
                        var rightPanel = new StackPanel { Orientation = Orientation.Horizontal };
                        rightPanel.Children.Add(signalBlock);
                        if (net.IsSecured)
                        {
                            rightPanel.Children.Add(new TextBlock
                            {
                                Text = " 🔒", Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                                FontSize = 10, VerticalAlignment = System.Windows.VerticalAlignment.Center
                            });
                        }
                        DockPanel.SetDock(rightPanel, Dock.Right);
                        panel.Children.Insert(0, rightPanel);
                        row.Child = panel;

                        var capturedNet = net;
                        row.MouseEnter += (s, a) => row.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                        row.MouseLeave += (s, a) => row.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        row.MouseLeftButtonDown += (s, a) =>
                        {
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

            // List audio devices
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
                            Cursor = System.Windows.Input.Cursors.Hand,
                            Background = dev.IsDefault
                                ? new SolidColorBrush(Color.FromArgb(25, 76, 217, 100))
                                : new SolidColorBrush(Color.FromArgb(0, 255, 255, 255))
                        };
                        var text = new TextBlock
                        {
                            Text = dev.Name + (dev.IsDefault ? " ✓" : ""),
                            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                            FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        row.Child = text;
                        row.MouseEnter += (s, a) => row.Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
                        row.MouseLeave += (s, a) => row.Background = dev.IsDefault
                            ? new SolidColorBrush(Color.FromArgb(25, 76, 217, 100))
                            : new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));
                        row.MouseLeftButtonDown += (s, a) =>
                        {
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
            if (_suppressVolumeEvent || !_windowLoaded) return;
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
            SetSystemIconHighlight(WifiIcon, false);
            SetSystemIconHighlight(VolumeIcon, false);
            SetSystemIconHighlight(AppsButton, false);
        }
    }
}
