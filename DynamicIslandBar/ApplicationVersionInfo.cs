using System.Reflection;
using System.Runtime.InteropServices;

namespace DynamicIslandBar;

public sealed record ApplicationVersionInfo(
    string ProductName,
    string Version,
    string Runtime,
    string OperatingSystem,
    string Architecture)
{
    public string ToClipboardText()
    {
        return $"{ProductName} {Version}{Environment.NewLine}"
            + $"运行时：{Runtime}{Environment.NewLine}"
            + $"系统：{OperatingSystem}{Environment.NewLine}"
            + $"架构：{Architecture}";
    }
}

public static class ApplicationVersionInfoProvider
{
    public static ApplicationVersionInfo GetCurrent()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ApplicationVersionInfoProvider).Assembly;
        var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? assembly.GetName().Name
            ?? "DynamicIslandBar";
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion.Split('+')[0]
            : assembly.GetName().Version?.ToString() ?? "未知";

        return new ApplicationVersionInfo(
            productName,
            version,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
    }
}
