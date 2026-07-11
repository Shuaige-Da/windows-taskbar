using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class ControlCenterTests
{
    [Fact]
    public void SettingsCoordinator_AppliesChangesImmediatelyAndFlushesOnePendingSave()
    {
        var config = new CapsuleConfig { Mode = CapsuleMode.TopIsland };
        var changes = new List<CapsuleSettingsChangeKind>();
        var saveCount = 0;
        using var coordinator = new CapsuleSettingsCoordinator(
            config,
            changes.Add,
            _ => saveCount++);

        coordinator.SetTheme(CapsuleThemePreset.GlassGreen);
        coordinator.SetStartupDisplayMode(StartupDisplayMode.CapsuleOnly);
        coordinator.SetCapsuleLength(64);
        coordinator.SetPartVisibility(CapsuleVisualPart.Lyrics, false);
        coordinator.SetPartOpacity(CapsuleVisualPart.Dock, 140);
        coordinator.SetPartVisibility(CapsuleVisualPart.CenterCard, false);
        coordinator.SetPartOpacity(CapsuleVisualPart.CenterCard, 72);
        coordinator.Flush();

        Assert.Equal(CapsuleThemePreset.GlassGreen, config.ThemePreset);
        Assert.Equal(StartupDisplayMode.CapsuleOnly, config.StartupDisplayMode);
        Assert.Equal(64, config.TopDockCapsuleLengthPercent);
        Assert.False(config.Presentation.Lyrics.IsVisible);
        Assert.False(config.Presentation.CenterCard.IsVisible);
        Assert.Equal(72, config.Presentation.CenterCard.OpacityPercent);
        Assert.Equal(100, config.Presentation.Dock.OpacityPercent);
        Assert.Contains(CapsuleSettingsChangeKind.Theme, changes);
        Assert.Contains(CapsuleSettingsChangeKind.Startup, changes);
        Assert.Contains(CapsuleSettingsChangeKind.Layout, changes);
        Assert.Contains(CapsuleSettingsChangeKind.Presentation, changes);
        Assert.Equal(1, saveCount);

        coordinator.Flush();
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void SettingsCoordinator_UsesFloatingLengthForBottomAndFloatingModes()
    {
        var config = new CapsuleConfig { Mode = CapsuleMode.Floating };
        using var coordinator = new CapsuleSettingsCoordinator(config, _ => { }, _ => { });

        coordinator.SetCapsuleLength(37);

        Assert.Equal(37, config.CapsuleLengthPercent);
        Assert.Equal(0, config.TopDockCapsuleLengthPercent);
        Assert.Equal(37, coordinator.CurrentCapsuleLengthPercent);
    }

    [Fact]
    public void FeedbackAttachmentPolicy_AcceptsSupportedImageWithinLimits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, new byte[128]);

            var accepted = FeedbackAttachmentPolicy.TryCreate(
                path,
                existingCount: 0,
                out var attachment,
                out var error);

            Assert.True(accepted, error);
            Assert.NotNull(attachment);
            Assert.Equal(Path.GetFileName(path), attachment.FileName);
            Assert.Equal(128, attachment.FileSizeBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FeedbackAttachmentPolicy_RejectsUnsupportedAndExcessImages()
    {
        Assert.False(FeedbackAttachmentPolicy.TryCreate(
            "missing.txt",
            existingCount: FeedbackAttachmentPolicy.MaximumImageCount,
            out _,
            out var countError));
        Assert.Contains("最多", countError);

        var path = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "not an image");
            Assert.False(FeedbackAttachmentPolicy.TryCreate(path, 0, out _, out var typeError));
            Assert.Contains("仅支持", typeError);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ApplicationVersionInfo_FormatsShareableEnvironmentText()
    {
        var info = new ApplicationVersionInfo(
            "DynamicIslandBar",
            "1.2.3",
            ".NET 10",
            "Windows",
            "X64");

        var text = info.ToClipboardText();

        Assert.Contains("DynamicIslandBar 1.2.3", text);
        Assert.Contains("运行时：.NET 10", text);
        Assert.Contains("系统：Windows", text);
        Assert.Contains("架构：X64", text);
    }

    [Fact]
    public void StartupRegistration_BuildsQuotedExecutableCommand()
    {
        var command = StartupRegistrationService.BuildCommandLine(
            @"C:\Program Files\Dynamic Island\DynamicIslandBar.exe");

        Assert.Equal(
            "\"C:\\Program Files\\Dynamic Island\\DynamicIslandBar.exe\"",
            command);
        Assert.Equal(
            "\"C:\\Apps\\DynamicIslandBar.exe\"",
            StartupRegistrationService.BuildCommandLine("\"C:\\Apps\\DynamicIslandBar.exe\""));
    }

    [Fact]
    public void ControlCenter_XamlContainsFourPagesAndFeedbackAttachmentSurface()
    {
        var xaml = ReadProjectFile("DynamicIslandBar", "CapsuleControlCenterWindow.xaml");
        var code = ReadProjectFile("DynamicIslandBar", "CapsuleControlCenterWindow.xaml.cs");

        Assert.Contains("x:Name=\"ThemePage\"", xaml);
        Assert.Contains("x:Name=\"SettingsPage\"", xaml);
        Assert.Contains("x:Name=\"VersionPage\"", xaml);
        Assert.Contains("x:Name=\"FeedbackPage\"", xaml);
        Assert.Contains("x:Name=\"FeedbackTextBox\"", xaml);
        Assert.Contains("AllowDrop=\"True\"", xaml);
        Assert.Contains("反馈渠道尚未配置", xaml);
        Assert.Contains("x:Name=\"StartupCheckBox\"", xaml);
        Assert.Contains("登录 Windows 后自动启动胶囊软件", xaml);
        Assert.Contains("x:Name=\"StartupDisplayModeComboBox\"", xaml);
        Assert.Contains("Content=\"导出配置\"", xaml);
        Assert.Contains("Content=\"导入配置\"", xaml);
        Assert.Contains("Content=\"恢复默认\"", xaml);
        Assert.Contains("Content=\"复制诊断信息\"", xaml);
        Assert.Contains("Content=\"打开日志目录\"", xaml);
        Assert.Contains("Content=\"清理日志\"", xaml);
        Assert.Contains("[CapsuleVisualPart.CenterCard] = \"中心卡片背景\"", code);
    }

    [Fact]
    public void ApplicationStartup_ShowsCapsuleAndControlCenter()
    {
        var appCode = ReadProjectFile("DynamicIslandBar", "App.xaml.cs");
        var mainWindowCode = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("mainWindow.Show();", appCode);
        Assert.Contains("mainWindow.OpenControlCenter,", appCode);
        Assert.Contains("DispatcherPriority.Loaded", appCode);
        Assert.Contains("private CapsuleControlCenterWindow? _controlCenterWindow;", mainWindowCode);
        Assert.Contains("if (_controlCenterWindow is { IsVisible: true })", mainWindowCode);
        Assert.Contains("Header = \"打开主页\"", mainWindowCode);
    }

    [Fact]
    public void SingleInstanceCoordinator_AllowsOnlyOneOwnerForTheSameName()
    {
        var instanceName = $"DynamicIslandBar-Tests-{Guid.NewGuid():N}";
        Assert.True(SingleInstanceCoordinator.TryAcquire(instanceName, out var primary));
        try
        {
            Assert.NotNull(primary);
            Assert.False(SingleInstanceCoordinator.TryAcquire(instanceName, out var duplicate));
            Assert.Null(duplicate);
        }
        finally
        {
            primary?.Dispose();
        }
    }

    [Fact]
    public void Program_UsesSingleInstanceAndAppCreatesTrayIcon()
    {
        var programCode = ReadProjectFile("DynamicIslandBar", "Program.cs");
        var appCode = ReadProjectFile("DynamicIslandBar", "App.xaml.cs");
        var trayCode = ReadProjectFile("DynamicIslandBar", "TrayIconService.cs");

        Assert.Contains("SingleInstanceCoordinator.TryAcquire", programCode);
        Assert.Contains("SingleInstanceCoordinator.SignalExistingInstance", programCode);
        Assert.Contains("new TrayIconService(", appCode);
        Assert.Contains("mainWindow.ShowCapsuleAndControlCenter", appCode);
        Assert.Contains("打开控制中心", trayCode);
        Assert.Contains("隐藏胶囊", trayCode);
        Assert.Contains("开机自启", trayCode);
        Assert.Contains("退出程序", trayCode);
    }

    [Fact]
    public void DiagnosticLogStore_RotatesAndClearsBoundedLogFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"DynamicIslandBar-Logs-{Guid.NewGuid():N}");
        try
        {
            var store = new DiagnosticLogStore(directory, maximumBytes: 1024, backupCount: 2);
            store.Append(new string('A', 700));
            store.Append(new string('B', 700));

            Assert.True(File.Exists(Path.Combine(directory, "app.log")));
            Assert.True(File.Exists(Path.Combine(directory, "app.log.1")));
            Assert.Contains(store.ReadRecentLines(10), line => line.Contains('B'));

            store.Clear();
            Assert.False(File.Exists(Path.Combine(directory, "app.log")));
            Assert.False(File.Exists(Path.Combine(directory, "app.log.1")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Diagnostics_ExceptionSummaryDoesNotIncludeExceptionMessageOrFilePath()
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException(@"private song at C:\Users\Someone\secret.txt");
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        var summary = AppDiagnostics.BuildExceptionSummary(exception);

        Assert.Contains(typeof(InvalidOperationException).FullName!, summary);
        Assert.DoesNotContain("private song", summary);
        Assert.DoesNotContain(@"C:\Users\Someone", summary);
        Assert.DoesNotContain(":line", summary);
    }

    [Fact]
    public void App_ConnectsProcessUiAndTaskExceptionDiagnostics()
    {
        var appCode = ReadProjectFile("DynamicIslandBar", "App.xaml.cs");

        Assert.Contains("AppDomain.CurrentDomain.UnhandledException +=", appCode);
        Assert.Contains("TaskScheduler.UnobservedTaskException +=", appCode);
        Assert.Contains("AppDiagnostics.Error(\"Dispatcher\"", appCode);
        Assert.Contains("AppDiagnostics.Error(\"AppDomain\"", appCode);
        Assert.Contains("AppDiagnostics.Error(\"TaskScheduler\"", appCode);
        Assert.Contains("e.SetObserved();", appCode);
    }

    private static string ReadProjectFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "DynamicIslandBar", "MainWindow.xaml")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(new[] { directory!.FullName }.Concat(pathParts).ToArray()));
    }
}
