using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class TaskbarRestoreWatchdogTests
{
    [Fact]
    public void BuildWatcherArguments_IncludesParentProcessId()
    {
        var args = TaskbarRestoreWatchdog.BuildWatcherArguments(1234);

        Assert.Equal("--restore-taskbar-when-parent-exits 1234", args);
    }

    [Fact]
    public void TryGetParentProcessId_RecognizesWatcherInvocation()
    {
        var matched = TaskbarRestoreWatchdog.TryGetParentProcessId(
            ["--restore-taskbar-when-parent-exits", "4321"],
            out var parentProcessId);

        Assert.True(matched);
        Assert.Equal(4321, parentProcessId);
    }

    [Fact]
    public void TryGetParentProcessId_RejectsNormalAppStartup()
    {
        var matched = TaskbarRestoreWatchdog.TryGetParentProcessId([], out var parentProcessId);

        Assert.False(matched);
        Assert.Equal(0, parentProcessId);
    }
}
