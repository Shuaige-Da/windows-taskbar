using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class StartupEnvironmentTests
{
    [Fact]
    public void ResolveWindowsDirectory_UsesWindirWhenPresent()
    {
        var path = StartupEnvironment.ResolveWindowsDirectory(
            @"C:\Windows",
            @"D:\Fallback");

        Assert.Equal(@"C:\Windows", path);
    }

    [Fact]
    public void ResolveWindowsDirectory_FallsBackToSystemRootWhenWindirIsMissing()
    {
        var path = StartupEnvironment.ResolveWindowsDirectory(
            null,
            @"C:\Windows");

        Assert.Equal(@"C:\Windows", path);
    }
}
