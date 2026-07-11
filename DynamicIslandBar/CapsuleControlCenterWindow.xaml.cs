using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIslandBar;

public partial class CapsuleControlCenterWindow : Window
{
    private enum PresentationControlKind
    {
        Visibility,
        AutoHide
    }

    private sealed record PresentationControlTag(
        CapsuleVisualPart Part,
        PresentationControlKind Kind);

    private sealed record ControlCenterThemeColors(
        Color Accent,
        Color Muted,
        Color Overlay);

    private sealed record SectionDescriptor(string Key, string Title, string IconGlyph, FrameworkElement Target);

    private sealed record FeatureSearchEntry(
        string Title,
        string Keywords,
        string PageKey,
        string SectionKey)
    {
        public string Location => $"{PageMetadata[PageKey].Title} / {Title}";
    }

    private static readonly Uri DefaultLandscapeUri = new(
        "pack://application:,,,/DynamicIslandBar;component/Assets/ControlCenter-DefaultLandscape.png",
        UriKind.Absolute);

    private static ImageSource? _defaultLandscapeSource;

    private static readonly IReadOnlyDictionary<string, (string Title, string Subtitle)> PageMetadata =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["Theme"] = ("主题", "选择胶囊的整体视觉风格"),
            ["Settings"] = ("设置", "调整外观、流光、歌词和功能区域"),
            ["Version"] = ("版本信息", "查看当前软件与运行环境"),
            ["Feedback"] = ("反馈", "提交文字和图片，帮助改进胶囊体验")
        };

    private static readonly IReadOnlyDictionary<CapsuleVisualPart, string> PartDisplayNames =
        new Dictionary<CapsuleVisualPart, string>
        {
            [CapsuleVisualPart.Chrome] = "胶囊背景",
            [CapsuleVisualPart.Dock] = "Dock 应用区",
            [CapsuleVisualPart.System] = "系统状态区",
            [CapsuleVisualPart.CenterCard] = "中心卡片背景",
            [CapsuleVisualPart.Lyrics] = "歌词区域",
            [CapsuleVisualPart.Details] = "详情区域",
            [CapsuleVisualPart.MediaControls] = "媒体控制区"
        };

    private static readonly IReadOnlyList<FeatureSearchEntry> FeatureSearchCatalog =
    [
        new("主题预设", "经典 玻璃 绿色 浅色 外观 theme", "Theme", "ThemePresets"),
        new("实时预览", "胶囊 主题 预览 preview", "Theme", "ThemePreview"),
        new("控制中心背景", "主页 山水 自定义 透明 图片", "Theme", "ControlCenterBackground"),
        new("胶囊背景", "胶囊 图片 透明度 填充", "Theme", "CapsuleBackground"),
        new("系统", "开机自启 启动 控制中心", "Settings", "System"),
        new("外观", "透明度 阴影 粗细 长度 中心卡片", "Settings", "Appearance"),
        new("流光", "亮度 粗细 速度 跑马灯", "Settings", "Glow"),
        new("歌词", "语言 简体 繁体", "Settings", "Lyrics"),
        new("功能区域", "显示 自动隐藏 透明度 Dock 系统 歌词 详情 媒体", "Settings", "Presentation"),
        new("配置管理", "导出 导入 恢复默认 备份", "Settings", "Configuration"),
        new("版本信息", ".NET 系统 架构 版本", "Version", "VersionInfo"),
        new("诊断中心", "日志 复制 清理 诊断", "Version", "Diagnostics"),
        new("文字反馈", "建议 问题 功能 反馈", "Feedback", "FeedbackText"),
        new("图片附件", "上传 图片 拖入 截图", "Feedback", "FeedbackAttachments")
    ];

    private readonly CapsuleSettingsCoordinator _settings;
    private readonly FeedbackDraft _feedbackDraft = new();
    private readonly ApplicationVersionInfo _versionInfo;
    private bool _isInitializing;
    private bool _isProgrammaticScroll;
    private string _currentPageKey = "Theme";
    private string? _selectedSectionKey;

    internal CapsuleControlCenterWindow(
        CapsuleConfig config,
        Action<CapsuleSettingsChangeKind> applyChange)
    {
        InitializeComponent();
        _settings = new CapsuleSettingsCoordinator(config, applyChange);
        _versionInfo = ApplicationVersionInfoProvider.GetCurrent();
        _isInitializing = true;
        try
        {
            BuildPresentationSettings();
        }
        finally
        {
            _isInitializing = false;
        }
        LoadVersionInformation();
        RefreshSettingsFromConfig();
        ShowPage("Theme");
        Activated += (_, _) => RefreshSettingsFromConfig();
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.Dispose();
        _feedbackDraft.Images.Clear();
        FeedbackAttachmentsPanel.Children.Clear();
        base.OnClosed(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizedState();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizedState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ExitApplicationButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "确定要退出胶囊程序吗？",
            "退出程序",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }

    private void ControlCenterWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ControlCenterRoot == null
            || ControlCenterRoot.ActualWidth <= 0
            || ControlCenterRoot.ActualHeight <= 0)
        {
            return;
        }

        ControlCenterRoot.Clip = new RectangleGeometry(
            new Rect(0, 0, ControlCenterRoot.ActualWidth, ControlCenterRoot.ActualHeight),
            radiusX: 18,
            radiusY: 18);
    }

    private void ToggleMaximizedState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string pageKey })
        {
            ShowPage(pageKey);
        }
    }

    private void ShowPage(string pageKey)
    {
        _currentPageKey = pageKey;
        ThemePage.Visibility = pageKey == "Theme" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = pageKey == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        VersionPage.Visibility = pageKey == "Version" ? Visibility.Visible : Visibility.Collapsed;
        FeedbackPage.Visibility = pageKey == "Feedback" ? Visibility.Visible : Visibility.Collapsed;

        if (PageMetadata.TryGetValue(pageKey, out var metadata))
        {
            PageTitleText.Text = metadata.Title;
            PageSubtitleText.Text = metadata.Subtitle;
        }

        foreach (var button in new[] { ThemeNavButton, SettingsNavButton, VersionNavButton, FeedbackNavButton })
        {
            var selected = string.Equals(button.Tag as string, pageKey, StringComparison.Ordinal);
            button.Background = selected
                ? (Brush)FindResource("GlassPillBrush")
                : Brushes.Transparent;
            button.BorderBrush = selected
                ? (Brush)FindResource("GlassPillBorderBrush")
                : Brushes.Transparent;
            button.Foreground = selected
                ? new SolidColorBrush(Color.FromRgb(0x2D, 0x72, 0xC7))
                : new SolidColorBrush(Color.FromRgb(0x36, 0x53, 0x6F));
        }

        BuildTopSectionNavigation(pageKey);
    }

    private IReadOnlyList<SectionDescriptor> GetSections(string pageKey) => pageKey switch
    {
        "Theme" =>
        [
            new("ThemePresets", "主题预设", "\uE790", ThemePresetsSection),
            new("ThemePreview", "实时预览", "\uE7B3", ThemePreviewSection),
            new("ControlCenterBackground", "控制中心背景", "\uE91B", ControlCenterBackgroundSection),
            new("CapsuleBackground", "胶囊背景", "\uE91B", CapsuleBackgroundSection)
        ],
        "Settings" =>
        [
            new("System", "系统", "\uE713", SystemSection),
            new("Appearance", "外观", "\uE771", AppearanceSection),
            new("Glow", "流光", "\uE706", GlowSection),
            new("Lyrics", "歌词", "\uE8D6", LyricsSection),
            new("Presentation", "功能区域", "\uECA5", PresentationSection),
            new("Configuration", "配置管理", "\uE8B7", ConfigurationSection)
        ],
        "Version" =>
        [
            new("VersionInfo", "版本信息", "\uE946", VersionInfoSection),
            new("Diagnostics", "诊断中心", "\uE9D9", DiagnosticsSection)
        ],
        "Feedback" =>
        [
            new("FeedbackText", "文字反馈", "\uE8BD", FeedbackTextSection),
            new("FeedbackAttachments", "图片附件", "\uEB9F", FeedbackDropZone)
        ],
        _ => []
    };

    private void BuildTopSectionNavigation(string pageKey)
    {
        TopSectionNavigationPanel.Children.Clear();
        foreach (var section in GetSections(pageKey))
        {
            var icon = new TextBlock
            {
                Text = section.IconGlyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = section.Title,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(icon);
            content.Children.Add(label);
            var button = new Button
            {
                Tag = section.Key,
                Content = content,
                Style = (Style)FindResource("TopSectionButtonStyle")
            };
            button.Click += TopSectionButton_Click;
            TopSectionNavigationPanel.Children.Add(button);
        }

        SelectTopSection(GetSections(pageKey).FirstOrDefault()?.Key);
    }

    private void TopSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionKey })
        {
            ScrollToSection(_currentPageKey, sectionKey);
        }
    }

    private ScrollViewer GetPageScrollViewer(string pageKey) => pageKey switch
    {
        "Settings" => SettingsPage,
        "Version" => VersionPage,
        "Feedback" => FeedbackPage,
        _ => ThemePage
    };

    private void ScrollToSection(string pageKey, string sectionKey)
    {
        var section = GetSections(pageKey).FirstOrDefault(item => item.Key == sectionKey);
        var scrollViewer = GetPageScrollViewer(pageKey);
        if (section is null || scrollViewer.Content is not FrameworkElement content)
        {
            return;
        }

        _isProgrammaticScroll = true;
        var y = section.Target.TranslatePoint(new Point(0, 0), content).Y;
        scrollViewer.ScrollToVerticalOffset(Math.Max(0, y - 4));
        SelectTopSection(sectionKey);
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _isProgrammaticScroll = false);
    }

    private void PageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isProgrammaticScroll || sender is not ScrollViewer scrollViewer || scrollViewer.Visibility != Visibility.Visible
            || scrollViewer.Content is not FrameworkElement content)
        {
            return;
        }

        var sections = GetSections(_currentPageKey);
        var selected = sections.FirstOrDefault();
        foreach (var section in sections)
        {
            var y = section.Target.TranslatePoint(new Point(0, 0), content).Y;
            if (y <= scrollViewer.VerticalOffset + 36)
            {
                selected = section;
            }
        }
        SelectTopSection(selected?.Key);
    }

    private void SelectTopSection(string? sectionKey)
    {
        _selectedSectionKey = sectionKey;
        foreach (var button in TopSectionNavigationPanel.Children.OfType<Button>())
        {
            var selected = string.Equals(button.Tag as string, sectionKey, StringComparison.Ordinal);
            button.Background = selected
                ? (Brush)FindResource("GlassPillBrush")
                : Brushes.Transparent;
            button.BorderBrush = selected
                ? (Brush)FindResource("GlassPillBorderBrush")
                : Brushes.Transparent;
            button.BorderThickness = selected ? new Thickness(1) : new Thickness(0);
            button.Foreground = new SolidColorBrush(selected
                ? Color.FromRgb(0x2D, 0x72, 0xC7)
                : Color.FromRgb(0x31, 0x4C, 0x68));
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string presetName }
            || !Enum.TryParse<CapsuleThemePreset>(presetName, out var preset))
        {
            return;
        }

        _settings.SetTheme(preset);
        ApplyControlCenterTheme();
        UpdateThemeSelection();
        ShowPage("Theme");
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        FeatureSearchPopup.IsOpen = true;
        FeatureSearchTextBox.Text = string.Empty;
        RefreshFeatureSearchResults();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, FeatureSearchTextBox.Focus);
    }

    private void FeatureSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFeatureSearchResults();
    }

    private void RefreshFeatureSearchResults()
    {
        if (FeatureSearchResultsList is null || FeatureSearchTextBox is null)
        {
            return;
        }

        var query = FeatureSearchTextBox.Text.Trim();
        var results = FeatureSearchCatalog
            .Where(entry => query.Length == 0
                || entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase)
                || PageMetadata[entry.PageKey].Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        FeatureSearchResultsList.ItemsSource = results;
        FeatureSearchEmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FeatureSearchResultsList.SelectedIndex = results.Count > 0 ? 0 : -1;
    }

    private void FeatureSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FeatureSearchPopup.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ActivateSelectedSearchResult();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && FeatureSearchResultsList.Items.Count > 0)
        {
            FeatureSearchResultsList.Focus();
            e.Handled = true;
        }
    }

    private void FeatureSearchResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelectedSearchResult();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FeatureSearchPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void FeatureSearchResultsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ActivateSelectedSearchResult();
    }

    private void ActivateSelectedSearchResult()
    {
        if (FeatureSearchResultsList.SelectedItem is not FeatureSearchEntry entry)
        {
            return;
        }

        FeatureSearchPopup.IsOpen = false;
        ShowPage(entry.PageKey);
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () => ScrollToSection(entry.PageKey, entry.SectionKey));
    }

    private void RefreshSettingsFromConfig()
    {
        if (!IsLoaded && _isInitializing)
        {
            return;
        }

        _isInitializing = true;
        try
        {
            var config = _settings.Config;
            var startupEnabled = StartupRegistrationService.IsEnabled();
            StartupCheckBox.IsChecked = startupEnabled;
            StartupStatusText.Text = startupEnabled ? "已启用" : "未启用";
            StartupStatusText.Foreground = startupEnabled
                ? new SolidColorBrush(Color.FromRgb(0x72, 0xF4, 0xA4))
                : new SolidColorBrush(Color.FromRgb(0x91, 0xA1, 0xB7));
            StartupDisplayModeComboBox.SelectedIndex = config.StartupDisplayMode == StartupDisplayMode.CapsuleOnly
                ? 1
                : 0;
            SetSlider(GlassOpacitySlider, GlassOpacityValue, config.GlassOpacityPercent);
            SetSlider(ShadowSlider, ShadowValue, config.ShadowPercent);
            SetSlider(CapsuleThicknessSlider, CapsuleThicknessValue, config.CapsuleThicknessPercent);
            SetSlider(CapsuleLengthSlider, CapsuleLengthValue, _settings.CurrentCapsuleLengthPercent);
            SetSlider(CenterCardWidthSlider, CenterCardWidthValue, config.CenterCardWidthPercent);
            SetSlider(GlowIntensitySlider, GlowIntensityValue, config.GlowIntensityPercent);
            SetSlider(GlowThicknessSlider, GlowThicknessValue, config.GlowThicknessPercent);
            SetSlider(GlowSpeedSlider, GlowSpeedValue, config.GlowSpeedPercent);
            LyricLanguageComboBox.SelectedIndex = config.LyricLanguage == LyricLanguage.Traditional ? 1 : 0;
            ApplyControlCenterTheme();
            UpdateThemeSelection();
            RefreshBackgroundImageSettings();
            RefreshPresentationSettings();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private static void SetSlider(Slider slider, TextBlock valueText, int value)
    {
        slider.Value = value;
        valueText.Text = $"{value}%";
    }

    private void SettingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing || sender is not Slider { Tag: string settingKey } slider)
        {
            return;
        }

        var percent = (int)Math.Round(slider.Value);
        switch (settingKey)
        {
            case "GlassOpacity":
                GlassOpacityValue.Text = $"{percent}%";
                _settings.SetGlassOpacity(percent);
                break;
            case "Shadow":
                ShadowValue.Text = $"{percent}%";
                _settings.SetShadow(percent);
                break;
            case "CapsuleThickness":
                CapsuleThicknessValue.Text = $"{percent}%";
                _settings.SetCapsuleThickness(percent);
                break;
            case "CapsuleLength":
                CapsuleLengthValue.Text = $"{percent}%";
                _settings.SetCapsuleLength(percent);
                break;
            case "CenterCardWidth":
                CenterCardWidthValue.Text = $"{percent}%";
                _settings.SetCenterCardWidth(percent);
                break;
            case "GlowIntensity":
                GlowIntensityValue.Text = $"{percent}%";
                _settings.SetGlowIntensity(percent);
                break;
            case "GlowThickness":
                GlowThicknessValue.Text = $"{percent}%";
                _settings.SetGlowThickness(percent);
                break;
            case "GlowSpeed":
                GlowSpeedValue.Text = $"{percent}%";
                _settings.SetGlowSpeed(percent);
                break;
        }
    }

    private void LyricLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || LyricLanguageComboBox.SelectedItem is not ComboBoxItem { Tag: string languageName })
        {
            return;
        }

        _settings.SetLyricLanguage(languageName == "Traditional"
            ? LyricLanguage.Traditional
            : LyricLanguage.Simplified);
    }

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        var requestedEnabled = StartupCheckBox.IsChecked == true;
        var result = StartupRegistrationService.SetEnabled(requestedEnabled);
        _isInitializing = true;
        try
        {
            StartupCheckBox.IsChecked = result.IsEnabled;
        }
        finally
        {
            _isInitializing = false;
        }

        StartupStatusText.Text = result.Success
            ? (result.IsEnabled ? "已启用，下次登录 Windows 时自动启动。" : "已关闭开机自启。")
            : result.ErrorMessage ?? "更新开机自启失败。";
        StartupStatusText.Foreground = new SolidColorBrush(result.Success
            ? Color.FromRgb(0x72, 0xF4, 0xA4)
            : Color.FromRgb(0xFF, 0x8A, 0x8A));
    }

    private void StartupDisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing
            || StartupDisplayModeComboBox.SelectedItem is not ComboBoxItem { Tag: string modeName }
            || !Enum.TryParse<StartupDisplayMode>(modeName, out var mode))
        {
            return;
        }

        _settings.SetStartupDisplayMode(mode);
    }

    private void ExportConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Flush();
        var dialog = new SaveFileDialog
        {
            Title = "导出胶囊配置",
            Filter = "胶囊配置文件|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = $"DynamicIslandBar-config-{DateTime.Now:yyyyMMdd}.json"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var success = CapsuleConfigService.TryExport(
            _settings.Config,
            dialog.FileName,
            out var errorMessage);
        SetConfigurationStatus(
            success ? "配置已成功导出。" : errorMessage,
            success);
    }

    private void ImportConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入胶囊配置",
            Filter = "胶囊配置文件|*.json|所有文件|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!CapsuleConfigService.TryImport(dialog.FileName, out var imported, out var errorMessage))
        {
            SetConfigurationStatus(errorMessage, success: false);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            "导入会覆盖当前主题、布局和应用设置，是否继续？",
            "导入配置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.ReplaceConfiguration(imported!);
        RefreshSettingsFromConfig();
        SetConfigurationStatus("配置已导入并应用。", success: true);
    }

    private void ResetConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "确定恢复全部胶囊设置为默认值吗？此操作不会关闭开机自启。",
            "恢复默认设置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.ReplaceConfiguration(new CapsuleConfig());
        RefreshSettingsFromConfig();
        SetConfigurationStatus("已恢复默认设置。", success: true);
    }

    private void SetConfigurationStatus(string message, bool success)
    {
        ConfigurationStatusText.Text = message;
        ConfigurationStatusText.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(0x72, 0xF4, 0xA4)
            : Color.FromRgb(0xFF, 0x8A, 0x8A));
    }

    private void UpdateThemeSelection()
    {
        var cards = new[]
        {
            (Button: ClassicThemeButton, Badge: ClassicThemeCheckBadge),
            (Button: GreenThemeButton, Badge: GreenThemeCheckBadge),
            (Button: LightThemeButton, Badge: LightThemeCheckBadge)
        };
        foreach (var card in cards)
        {
            var selected = string.Equals(
                card.Button.Tag as string,
                _settings.Config.ThemePreset.ToString(),
                StringComparison.Ordinal);
            card.Button.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(0x4D, 0x8C, 0xFF))
                : (Brush)FindResource("CardBorderBrush");
            card.Button.BorderThickness = new Thickness(selected ? 2 : 1);
            card.Badge.Background = new SolidColorBrush(Color.FromRgb(0x4D, 0x8C, 0xFF));
            card.Badge.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void RefreshBackgroundImageSettings()
    {
        var config = _settings.Config;
        var hasCapsuleImage = CapsuleBackgroundImagePolicy.IsSupportedImagePath(config.BackgroundImagePath);
        BackgroundImageFileNameText.Text = hasCapsuleImage
            ? Path.GetFileName(config.BackgroundImagePath)
            : "未选择图片";
        BackgroundImagePreview.Source = hasCapsuleImage
            ? LoadThumbnail(config.BackgroundImagePath!)
            : null;

        var mode = config.ControlCenterBackgroundMode;
        var customImageIsValid = CapsuleBackgroundImagePolicy.IsSupportedImagePath(
            config.ControlCenterBackgroundImagePath);
        var defaultLandscape = LoadDefaultLandscape();
        ImageSource? effectiveSource;
        Stretch effectiveStretch;
        string fileName;
        string status;
        var statusIsSuccess = true;
        switch (mode)
        {
            case ControlCenterBackgroundMode.CustomImage when customImageIsValid:
                effectiveSource = TryLoadControlCenterBackground(config.ControlCenterBackgroundImagePath!);
                effectiveStretch = CapsuleBackgroundImagePolicy.MapStretch(config.ControlCenterBackgroundImageStretchMode);
                fileName = Path.GetFileName(config.ControlCenterBackgroundImagePath) ?? "自定义图片";
                status = "自定义主页背景已启用。";
                break;
            case ControlCenterBackgroundMode.CustomImage:
                effectiveSource = defaultLandscape;
                effectiveStretch = Stretch.UniformToFill;
                fileName = "自定义图片不可用 · 已临时回退默认山水";
                status = defaultLandscape is null
                    ? "自定义图片与默认资源均无法加载，已降级为透明模式。"
                    : "找不到自定义图片，当前临时显示默认山水；配置路径保持不变。";
                statusIsSuccess = false;
                break;
            case ControlCenterBackgroundMode.Transparent:
                effectiveSource = null;
                effectiveStretch = Stretch.UniformToFill;
                fileName = "完全透明（无图片）";
                status = "控制中心使用完全透明背景。";
                break;
            default:
                effectiveSource = defaultLandscape;
                effectiveStretch = Stretch.UniformToFill;
                fileName = defaultLandscape is null ? "默认资源加载失败" : "默认山水";
                status = defaultLandscape is null
                    ? "默认山水资源无法加载，已降级为透明模式。"
                    : "默认山水背景已启用。";
                statusIsSuccess = defaultLandscape is not null;
                break;
        }

        if (mode == ControlCenterBackgroundMode.CustomImage && effectiveSource is null)
        {
            effectiveSource = defaultLandscape;
            effectiveStretch = Stretch.UniformToFill;
            fileName = "自定义图片无法解码 · 已临时回退默认山水";
            status = defaultLandscape is null
                ? "自定义图片与默认资源均无法加载，已降级为透明模式。"
                : "自定义图片无法读取，当前临时显示默认山水；配置路径保持不变。";
            statusIsSuccess = false;
        }

        ControlCenterBackgroundImageFileNameText.Text = fileName;
        ControlCenterBackgroundImagePreview.Source = effectiveSource;
        ControlCenterBackgroundImage.Source = effectiveSource;
        ControlCenterBackgroundImage.Opacity = effectiveSource is null
            ? 0d
            : Math.Clamp(config.ControlCenterBackgroundImageOpacity, 0d, 1d);
        ControlCenterBackgroundImage.Stretch = effectiveStretch;
        SetControlCenterBackgroundImageStatus(status, statusIsSuccess);
        var controlCenterOpacityPercent = (int)Math.Round(
            Math.Clamp(config.ControlCenterBackgroundImageOpacity, 0d, 1d) * 100d);
        ControlCenterBackgroundImageOpacitySlider.Value = controlCenterOpacityPercent;
        ControlCenterBackgroundImageOpacityValue.Text = $"{controlCenterOpacityPercent}%";
        ControlCenterBackgroundImageStretchComboBox.SelectedIndex = CapsuleBackgroundImagePolicy.MapStretch(
            config.ControlCenterBackgroundImageStretchMode) switch
        {
            Stretch.Uniform => 1,
            Stretch.Fill => 2,
            _ => 0
        };
        ControlCenterBackgroundImageOpacitySlider.IsEnabled = mode != ControlCenterBackgroundMode.Transparent;
        ControlCenterBackgroundImageStretchComboBox.IsEnabled = mode == ControlCenterBackgroundMode.CustomImage;
        UpdateControlCenterBackgroundModeButtons(mode);

        var opacityPercent = (int)Math.Round(Math.Clamp(config.BackgroundImageOpacity, 0d, 1d) * 100d);
        BackgroundImageOpacitySlider.Value = opacityPercent;
        BackgroundImageOpacityValue.Text = $"{opacityPercent}%";
        BackgroundImageStretchComboBox.SelectedIndex = CapsuleBackgroundImagePolicy.MapStretch(
            config.BackgroundImageStretchMode) switch
        {
            Stretch.Uniform => 1,
            Stretch.Fill => 2,
            _ => 0
        };
    }

    private static ImageSource? TryLoadControlCenterBackground(string path)
    {
        try
        {
            return CapsuleBackgroundImagePolicy.LoadFrozenImageSource(path);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadDefaultLandscape()
    {
        if (_defaultLandscapeSource is not null)
        {
            return _defaultLandscapeSource;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = DefaultLandscapeUri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            _defaultLandscapeSource = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Error("ControlCenterDefaultLandscape", ex);
            return null;
        }
    }

    private void UpdateControlCenterBackgroundModeButtons(ControlCenterBackgroundMode selectedMode)
    {
        foreach (var button in new[] { DefaultLandscapeModeButton, CustomImageModeButton, TransparentModeButton })
        {
            var selected = Enum.TryParse<ControlCenterBackgroundMode>(button.Tag as string, out var mode)
                && mode == selectedMode;
            button.Background = selected
                ? (Brush)FindResource("GlassPillBrush")
                : Brushes.Transparent;
            button.BorderBrush = selected
                ? (Brush)FindResource("GlassPillBorderBrush")
                : Brushes.Transparent;
            button.BorderThickness = selected ? new Thickness(1) : new Thickness(0);
            button.Foreground = new SolidColorBrush(selected
                ? Color.FromRgb(0x2D, 0x72, 0xC7)
                : Color.FromRgb(0x36, 0x53, 0x6F));
        }
    }

    private void ApplyControlCenterTheme()
    {
        var colors = _settings.Config.ThemePreset switch
        {
            CapsuleThemePreset.GlassGreen => new ControlCenterThemeColors(
                Color.FromRgb(0x4C, 0xD9, 0x64),
                Color.FromRgb(0x58, 0x74, 0x6D),
                Color.FromArgb(0x52, 0xFF, 0xFF, 0xFF)),
            CapsuleThemePreset.SoftLight => new ControlCenterThemeColors(
                Color.FromRgb(0x8A, 0x7D, 0xFF),
                Color.FromRgb(0x62, 0x6F, 0x8E),
                Color.FromArgb(0x58, 0xFF, 0xFF, 0xFF)),
            _ => new ControlCenterThemeColors(
                Color.FromRgb(0x4D, 0x8C, 0xFF),
                Color.FromRgb(0x61, 0x78, 0x95),
                Color.FromArgb(0x52, 0xFF, 0xFF, 0xFF))
        };

        SetBrushColor("AccentBrush", colors.Accent);
        SetBrushColor("MutedTextBrush", colors.Muted);
        SetBrushColor("WindowOverlayBrush", colors.Overlay);
    }

    private void SetBrushColor(string resourceKey, Color color)
    {
        if (Resources[resourceKey] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
    }

    private void ChooseControlCenterBackgroundImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateBackgroundImageDialog("选择控制中心背景图片");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!TryValidateBackgroundImage(dialog.FileName, out var error))
        {
            SetControlCenterBackgroundImageStatus(error, success: false);
            return;
        }

        _settings.SetControlCenterBackgroundImage(dialog.FileName);
        _settings.SetControlCenterBackgroundMode(ControlCenterBackgroundMode.CustomImage);
        if (_settings.Config.ControlCenterBackgroundImageOpacity <= 0)
        {
            _settings.SetControlCenterBackgroundImageOpacity(100);
        }
        RefreshSettingsFromConfig();
        SetControlCenterBackgroundImageStatus("主页背景图片已应用。", success: true);
    }

    private void RemoveControlCenterBackgroundImageButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SetControlCenterBackgroundMode(ControlCenterBackgroundMode.Transparent);
        RefreshSettingsFromConfig();
        SetControlCenterBackgroundImageStatus("主页已恢复透明液体玻璃。", success: true);
    }

    private void ControlCenterBackgroundModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string modeName }
            || !Enum.TryParse<ControlCenterBackgroundMode>(modeName, out var mode))
        {
            return;
        }

        _settings.SetControlCenterBackgroundMode(mode);
        RefreshSettingsFromConfig();
        if (mode == ControlCenterBackgroundMode.CustomImage
            && !CapsuleBackgroundImagePolicy.IsSupportedImagePath(_settings.Config.ControlCenterBackgroundImagePath))
        {
            SetControlCenterBackgroundImageStatus("尚未选择有效的自定义图片，当前临时显示默认山水。", success: false);
        }
    }

    private void ControlCenterBackgroundImageOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
        {
            return;
        }

        var percent = (int)Math.Round(ControlCenterBackgroundImageOpacitySlider.Value);
        ControlCenterBackgroundImageOpacityValue.Text = $"{percent}%";
        if (_settings.Config.ControlCenterBackgroundMode != ControlCenterBackgroundMode.Transparent)
        {
            ControlCenterBackgroundImage.Opacity = percent / 100d;
        }
        _settings.SetControlCenterBackgroundImageOpacity(percent);
    }

    private void ControlCenterBackgroundImageStretchComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isInitializing
            || ControlCenterBackgroundImageStretchComboBox.SelectedItem
                is not ComboBoxItem { Tag: string stretchMode })
        {
            return;
        }

        ControlCenterBackgroundImage.Stretch = CapsuleBackgroundImagePolicy.MapStretch(stretchMode);
        _settings.SetControlCenterBackgroundImageStretchMode(stretchMode);
    }

    private void SetControlCenterBackgroundImageStatus(string message, bool success)
    {
        ControlCenterBackgroundImageStatusText.Text = message;
        ControlCenterBackgroundImageStatusText.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(0x72, 0xF4, 0xA4)
            : Color.FromRgb(0xFF, 0x8A, 0x8A));
    }

    private void ChooseBackgroundImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = CreateBackgroundImageDialog("选择胶囊背景图片");
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!TryValidateBackgroundImage(dialog.FileName, out var error))
        {
            SetBackgroundImageStatus(error, success: false);
            return;
        }

        _settings.SetBackgroundImage(dialog.FileName);
        if (_settings.Config.BackgroundImageOpacity <= 0)
        {
            _settings.SetBackgroundImageOpacity(45);
        }
        RefreshSettingsFromConfig();
        SetBackgroundImageStatus("背景图片已应用。", success: true);
    }

    private static OpenFileDialog CreateBackgroundImageDialog(string title)
    {
        return new OpenFileDialog
        {
            Title = title,
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
            Multiselect = false
        };
    }

    private static bool TryValidateBackgroundImage(string path, out string error)
    {
        if (!CapsuleBackgroundImagePolicy.IsSupportedImagePath(path))
        {
            error = "图片格式不受支持或文件无法读取。";
            return false;
        }
        if (LoadThumbnail(path) == null)
        {
            error = "图片内容无法解码，请选择其他图片。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RemoveBackgroundImageButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SetBackgroundImage(null);
        RefreshSettingsFromConfig();
        SetBackgroundImageStatus("背景图片已移除，主题底色保持不变。", success: true);
    }

    private void BackgroundImageOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing)
        {
            return;
        }

        var percent = (int)Math.Round(BackgroundImageOpacitySlider.Value);
        BackgroundImageOpacityValue.Text = $"{percent}%";
        _settings.SetBackgroundImageOpacity(percent);
    }

    private void BackgroundImageStretchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing
            || BackgroundImageStretchComboBox.SelectedItem is not ComboBoxItem { Tag: string stretchMode })
        {
            return;
        }

        _settings.SetBackgroundImageStretchMode(stretchMode);
    }

    private void SetBackgroundImageStatus(string message, bool success)
    {
        BackgroundImageStatusText.Text = message;
        BackgroundImageStatusText.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(0x72, 0xF4, 0xA4)
            : Color.FromRgb(0xFF, 0x8A, 0x8A));
    }

    private void BuildPresentationSettings()
    {
        PresentationSettingsPanel.Children.Clear();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        AddPresentationHeader(header, "区域", 0, HorizontalAlignment.Left);
        AddPresentationHeader(header, "显示", 1, HorizontalAlignment.Center);
        AddPresentationHeader(header, "自动隐藏", 2, HorizontalAlignment.Center);
        AddPresentationHeader(header, "能量 / 透明度", 3, HorizontalAlignment.Left);
        PresentationSettingsPanel.Children.Add(header);

        foreach (var part in Enum.GetValues<CapsuleVisualPart>())
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 8), Tag = part };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

            row.Children.Add(new TextBlock
            {
                Text = PartDisplayNames[part],
                VerticalAlignment = VerticalAlignment.Center
            });

            var visibilityCheckBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = $"控制{PartDisplayNames[part]}是否显示",
                Tag = new PresentationControlTag(part, PresentationControlKind.Visibility)
            };
            visibilityCheckBox.Checked += PresentationVisibility_Changed;
            visibilityCheckBox.Unchecked += PresentationVisibility_Changed;
            Grid.SetColumn(visibilityCheckBox, 1);
            row.Children.Add(visibilityCheckBox);

            var autoHideCheckBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = $"控制{PartDisplayNames[part]}是否随胶囊隐藏（自动隐藏）",
                Tag = new PresentationControlTag(part, PresentationControlKind.AutoHide)
            };
            autoHideCheckBox.Checked += PresentationAutoHide_Changed;
            autoHideCheckBox.Unchecked += PresentationAutoHide_Changed;
            Grid.SetColumn(autoHideCheckBox, 2);
            row.Children.Add(autoHideCheckBox);

            var opacitySlider = new Slider { Tag = part };
            opacitySlider.ValueChanged += PresentationOpacity_ValueChanged;
            Grid.SetColumn(opacitySlider, 3);
            row.Children.Add(opacitySlider);

            var opacityText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Value"
            };
            Grid.SetColumn(opacityText, 4);
            row.Children.Add(opacityText);
            PresentationSettingsPanel.Children.Add(row);
        }
    }

    private static void AddPresentationHeader(
        Grid header,
        string text,
        int column,
        HorizontalAlignment alignment)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0x91, 0xA8, 0xBC)),
            FontSize = 11,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, column);
        header.Children.Add(label);
    }

    private void RefreshPresentationSettings()
    {
        foreach (var row in PresentationSettingsPanel.Children.OfType<Grid>())
        {
            if (row.Tag is not CapsuleVisualPart part)
            {
                continue;
            }

            var preference = _settings.Config.Presentation.Get(part);
            var visibilityCheckBox = FindPresentationCheckBox(
                row,
                part,
                PresentationControlKind.Visibility);
            var autoHideCheckBox = FindPresentationCheckBox(
                row,
                part,
                PresentationControlKind.AutoHide);
            var slider = row.Children.OfType<Slider>().Single();
            var valueText = row.Children.OfType<TextBlock>().Single(text => Equals(text.Tag, "Value"));
            visibilityCheckBox.IsChecked = preference.IsVisible;
            autoHideCheckBox.IsChecked = preference.AutoHideWithCapsule;
            slider.Value = preference.OpacityPercent;
            valueText.Text = $"{preference.OpacityPercent}%";
        }
    }

    private static CheckBox FindPresentationCheckBox(
        Grid row,
        CapsuleVisualPart part,
        PresentationControlKind kind)
    {
        return row.Children
            .OfType<CheckBox>()
            .Single(checkBox => checkBox.Tag is PresentationControlTag tag
                && tag.Part == part
                && tag.Kind == kind);
    }

    private void PresentationVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing
            || sender is not CheckBox
            {
                Tag: PresentationControlTag
                {
                    Part: var part,
                    Kind: PresentationControlKind.Visibility
                }
            } checkBox)
        {
            return;
        }

        _settings.SetPartVisibility(part, checkBox.IsChecked == true);
    }

    private void PresentationAutoHide_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing
            || sender is not CheckBox
            {
                Tag: PresentationControlTag
                {
                    Part: var part,
                    Kind: PresentationControlKind.AutoHide
                }
            } checkBox)
        {
            return;
        }

        _settings.SetPartAutoHideWithCapsule(part, checkBox.IsChecked == true);
    }

    private void PresentationOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing || sender is not Slider { Tag: CapsuleVisualPart part } slider)
        {
            return;
        }

        var percent = (int)Math.Round(slider.Value);
        if (slider.Parent is Grid row)
        {
            row.Children.OfType<TextBlock>().Single(text => Equals(text.Tag, "Value")).Text = $"{percent}%";
        }
        _settings.SetPartOpacity(part, percent);
    }

    private void LoadVersionInformation()
    {
        ProductNameText.Text = _versionInfo.ProductName;
        ProductVersionText.Text = $"版本 {_versionInfo.Version}";
        RuntimeText.Text = _versionInfo.Runtime;
        OperatingSystemText.Text = _versionInfo.OperatingSystem;
        ArchitectureText.Text = _versionInfo.Architecture;
        SidebarVersionText.Text = $"v{_versionInfo.Version}";
    }

    private void CopyVersionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_versionInfo.ToClipboardText());
            CopyVersionStatusText.Text = "版本信息已复制。";
        }
        catch
        {
            CopyVersionStatusText.Text = "复制失败，请稍后重试。";
        }
    }

    private void CopyDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(AppDiagnostics.BuildReport(_settings.Config));
            SetDiagnosticsStatus("诊断信息已复制，可粘贴到反馈中。", success: true);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Error("DiagnosticsCopy", ex);
            SetDiagnosticsStatus("复制诊断信息失败。", success: false);
        }
    }

    private void OpenLogDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppDiagnostics.LogDirectory);
            Process.Start(new ProcessStartInfo(AppDiagnostics.LogDirectory)
            {
                UseShellExecute = true
            });
            SetDiagnosticsStatus("已打开日志目录。", success: true);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Error("DiagnosticsFolder", ex);
            SetDiagnosticsStatus("无法打开日志目录。", success: false);
        }
    }

    private void ClearDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "确定清理全部本地诊断日志吗？",
            "清理诊断日志",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        AppDiagnostics.Clear();
        SetDiagnosticsStatus("诊断日志已清理。", success: true);
    }

    private void SetDiagnosticsStatus(string message, bool success)
    {
        DiagnosticsStatusText.Text = message;
        DiagnosticsStatusText.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(0x72, 0xF4, 0xA4)
            : Color.FromRgb(0xFF, 0x8A, 0x8A));
    }

    private void FeedbackTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _feedbackDraft.Text = FeedbackTextBox.Text;
        FeedbackCountText.Text = $"{FeedbackTextBox.Text.Length} 字";
    }

    private void SelectFeedbackImagesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择反馈图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            AddFeedbackImages(dialog.FileNames);
        }
    }

    private void FeedbackDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void FeedbackDropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            AddFeedbackImages(files);
        }
    }

    private void AddFeedbackImages(IEnumerable<string> filePaths)
    {
        var errors = new List<string>();
        foreach (var filePath in filePaths)
        {
            if (_feedbackDraft.Images.Any(image => string.Equals(
                    image.FilePath,
                    Path.GetFullPath(filePath),
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!FeedbackAttachmentPolicy.TryCreate(
                    filePath,
                    _feedbackDraft.Images.Count,
                    out var attachment,
                    out var error))
            {
                errors.Add(error);
                continue;
            }

            _feedbackDraft.Images.Add(attachment!);
        }

        FeedbackStatusText.Text = string.Join(Environment.NewLine, errors.Distinct());
        RenderFeedbackAttachments();
    }

    private void RenderFeedbackAttachments()
    {
        FeedbackAttachmentsPanel.Children.Clear();
        foreach (var attachment in _feedbackDraft.Images)
        {
            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x70, 0x10, 0x1B, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0x48, 0x60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 3, 0, 3)
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            layout.Children.Add(new Image
            {
                Source = LoadThumbnail(attachment.FilePath),
                Width = 52,
                Height = 52,
                Stretch = Stretch.UniformToFill
            });
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = attachment.FileName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brushes.White
            });
            info.Children.Add(new TextBlock
            {
                Text = FormatFileSize(attachment.FileSizeBytes),
                Foreground = new SolidColorBrush(Color.FromRgb(0x91, 0xA1, 0xB7)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(info, 1);
            layout.Children.Add(info);

            var removeButton = new Button
            {
                Content = "删除",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x8A)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                Cursor = Cursors.Hand,
                Tag = attachment
            };
            removeButton.Click += RemoveFeedbackAttachment_Click;
            Grid.SetColumn(removeButton, 2);
            layout.Children.Add(removeButton);
            row.Child = layout;
            FeedbackAttachmentsPanel.Children.Add(row);
        }
    }

    private void RemoveFeedbackAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FeedbackImageAttachment attachment })
        {
            _feedbackDraft.Images.Remove(attachment);
            FeedbackStatusText.Text = string.Empty;
            RenderFeedbackAttachments();
        }
    }

    private static BitmapImage? LoadThumbnail(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 120;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:F1} MB"
            : $"{Math.Max(1, bytes / 1024d):F0} KB";
    }
}
