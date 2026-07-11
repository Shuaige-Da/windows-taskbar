using Microsoft.Win32;
using System.IO;

namespace DynamicIslandBar;

public readonly record struct StartupRegistrationResult(
    bool Success,
    bool IsEnabled,
    string? ErrorMessage = null);

public static class StartupRegistrationService
{
    internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "DynamicIslandBar";

    public static bool IsEnabled()
    {
        try
        {
            var executablePath = ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            var registeredCommand = key?.GetValue(ValueName) as string;
            return string.Equals(
                registeredCommand,
                BuildCommandLine(executablePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static StartupRegistrationResult SetEnabled(bool isEnabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
            if (isEnabled)
            {
                var executablePath = ResolveExecutablePath();
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return new StartupRegistrationResult(
                        Success: false,
                        IsEnabled: false,
                        ErrorMessage: "无法确定程序路径，未能启用开机自启。");
                }

                key.SetValue(
                    ValueName,
                    BuildCommandLine(executablePath),
                    RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return new StartupRegistrationResult(
                Success: true,
                IsEnabled: isEnabled);
        }
        catch (Exception ex)
        {
            return new StartupRegistrationResult(
                Success: false,
                IsEnabled: IsEnabled(),
                ErrorMessage: $"更新开机自启失败：{ex.Message}");
        }
    }

    internal static string BuildCommandLine(string executablePath)
    {
        return $"\"{executablePath.Trim().Trim('\"')}\"";
    }

    private static string? ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(processPath)
            ? null
            : Path.GetFullPath(processPath);
    }
}
