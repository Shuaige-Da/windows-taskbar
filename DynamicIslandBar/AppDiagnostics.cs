using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace DynamicIslandBar;

internal sealed class DiagnosticLogStore
{
    private readonly object _sync = new();
    private readonly string _directory;
    private readonly string _logPath;
    private readonly long _maximumBytes;
    private readonly int _backupCount;

    public DiagnosticLogStore(
        string directory,
        long maximumBytes = 1024 * 1024,
        int backupCount = 3)
    {
        _directory = directory;
        _logPath = Path.Combine(directory, "app.log");
        _maximumBytes = Math.Max(1024, maximumBytes);
        _backupCount = Math.Clamp(backupCount, 1, 10);
    }

    public string DirectoryPath => _directory;

    public void Append(string line)
    {
        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                var incomingBytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                if (File.Exists(_logPath)
                    && new FileInfo(_logPath).Length + incomingBytes > _maximumBytes)
                {
                    RotateFiles();
                }
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Diagnostics must never affect the application.
            }
        }
    }

    public IReadOnlyList<string> ReadRecentLines(int maximumLineCount)
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_logPath) || maximumLineCount <= 0)
                {
                    return [];
                }
                return File.ReadLines(_logPath)
                    .TakeLast(Math.Clamp(maximumLineCount, 1, 500))
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    File.Delete(_logPath);
                }
                for (var index = 1; index <= _backupCount; index++)
                {
                    var backupPath = GetBackupPath(index);
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
            }
            catch
            {
            }
        }
    }

    private void RotateFiles()
    {
        var oldest = GetBackupPath(_backupCount);
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var index = _backupCount - 1; index >= 1; index--)
        {
            var source = GetBackupPath(index);
            if (File.Exists(source))
            {
                File.Move(source, GetBackupPath(index + 1), overwrite: true);
            }
        }

        if (File.Exists(_logPath))
        {
            File.Move(_logPath, GetBackupPath(1), overwrite: true);
        }
    }

    private string GetBackupPath(int index) => $"{_logPath}.{index}";
}

public static class AppDiagnostics
{
    private static readonly DateTime ProcessStartedAtUtc = DateTime.UtcNow;
    private static readonly DiagnosticLogStore Store = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicIslandBar",
        "Logs"));

    public static string LogDirectory => Store.DirectoryPath;

    public static void Info(string area, string message) => Write("INFO", area, message);

    public static void Warning(string area, string message) => Write("WARN", area, message);

    public static void Error(string area, Exception exception)
    {
        Write("ERROR", area, BuildExceptionSummary(exception));
    }

    internal static string BuildExceptionSummary(Exception exception)
    {
        var stack = new StackTrace(exception, fNeedFileInfo: false).ToString();
        return $"exception={exception.GetType().FullName} "
            + $"hresult=0x{exception.HResult:X8} "
            + $"stack={Normalize(stack, 1600)}";
    }

    public static IReadOnlyList<string> ReadRecentLines(int maximumLineCount = 100) =>
        Store.ReadRecentLines(maximumLineCount);

    public static void Clear() => Store.Clear();

    public static string BuildReport(CapsuleConfig config)
    {
        var version = ApplicationVersionInfoProvider.GetCurrent();
        using var process = Process.GetCurrentProcess();
        var builder = new StringBuilder();
        builder.AppendLine("DynamicIslandBar 诊断报告");
        builder.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"版本：{version.Version}");
        builder.AppendLine($"运行时：{version.Runtime}");
        builder.AppendLine($"系统：{version.OperatingSystem}");
        builder.AppendLine($"架构：{version.Architecture}");
        builder.AppendLine($"运行时长：{FormatUptime(DateTime.UtcNow - ProcessStartedAtUtc)}");
        builder.AppendLine($"工作集：{process.WorkingSet64 / 1024d / 1024d:F1} MB");
        builder.AppendLine($"胶囊模式：{config.Mode}");
        builder.AppendLine($"主题：{config.ThemePreset}");
        builder.AppendLine($"启动显示：{config.StartupDisplayMode}");
        builder.AppendLine($"键盘导航：{config.IsKeyboardNavigationEnabled}");
        builder.AppendLine($"开机自启：{StartupRegistrationService.IsEnabled()}");
        builder.AppendLine();
        builder.AppendLine("最近日志：");
        var lines = ReadRecentLines(100);
        if (lines.Count == 0)
        {
            builder.AppendLine("(无)");
        }
        else
        {
            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }
        }
        return builder.ToString();
    }

    private static void Write(string level, string area, string message)
    {
        var safeArea = Normalize(area, 60);
        var safeMessage = Normalize(message, 2000);
        Store.Append($"{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)} [{level}] [{safeArea}] {safeMessage}");
    }

    private static string Normalize(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "(empty)"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        return $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
    }
}
