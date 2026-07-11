using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIslandBar;

public partial class CapsuleControlCenterWindow : Window
{
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

    private readonly CapsuleSettingsCoordinator _settings;
    private readonly FeedbackDraft _feedbackDraft = new();
    private readonly ApplicationVersionInfo _versionInfo;
    private bool _isInitializing;

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
            button.Background = new SolidColorBrush(selected
                ? Color.FromArgb(0xC8, 0x17, 0x32, 0x46)
                : Colors.Transparent);
            button.BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(0x88, 0x46, 0xE0, 0xFF)
                : Colors.Transparent);
            button.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xC8, 0xD4, 0xE4));
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
        UpdateThemeSelection();
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
            UpdateThemeSelection();
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
        foreach (var button in new[] { ClassicThemeButton, GreenThemeButton, LightThemeButton })
        {
            var selected = string.Equals(
                button.Tag as string,
                _settings.Config.ThemePreset.ToString(),
                StringComparison.Ordinal);
            button.BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(0x46, 0xE0, 0xFF)
                : Color.FromRgb(0x34, 0x4B, 0x65));
            button.BorderThickness = new Thickness(selected ? 2 : 1);
        }
    }

    private void BuildPresentationSettings()
    {
        PresentationSettingsPanel.Children.Clear();
        foreach (var part in Enum.GetValues<CapsuleVisualPart>())
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 8), Tag = part };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });

            row.Children.Add(new TextBlock
            {
                Text = PartDisplayNames[part],
                VerticalAlignment = VerticalAlignment.Center
            });

            var visibilityCheckBox = new CheckBox
            {
                Content = "显示",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = part
            };
            visibilityCheckBox.Checked += PresentationVisibility_Changed;
            visibilityCheckBox.Unchecked += PresentationVisibility_Changed;
            Grid.SetColumn(visibilityCheckBox, 1);
            row.Children.Add(visibilityCheckBox);

            var opacitySlider = new Slider { Tag = part };
            opacitySlider.ValueChanged += PresentationOpacity_ValueChanged;
            Grid.SetColumn(opacitySlider, 2);
            row.Children.Add(opacitySlider);

            var opacityText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = "Value"
            };
            Grid.SetColumn(opacityText, 3);
            row.Children.Add(opacityText);
            PresentationSettingsPanel.Children.Add(row);
        }
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
            var checkBox = row.Children.OfType<CheckBox>().Single();
            var slider = row.Children.OfType<Slider>().Single();
            var valueText = row.Children.OfType<TextBlock>().Single(text => Equals(text.Tag, "Value"));
            checkBox.IsChecked = preference.IsVisible;
            slider.Value = preference.OpacityPercent;
            valueText.Text = $"{preference.OpacityPercent}%";
        }
    }

    private void PresentationVisibility_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || sender is not CheckBox { Tag: CapsuleVisualPart part } checkBox)
        {
            return;
        }

        _settings.SetPartVisibility(part, checkBox.IsChecked == true);
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
