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

    [Fact]
    public void TryRestoreTaskbar_RejectsNormalAppStartup()
    {
        Assert.False(TaskbarRestoreWatchdog.TryRestoreTaskbar([]));
    }

    [Fact]
    public void App_UsesProgramEntryPointSoWatchdogAvoidsWpfStartup()
    {
        var projectFile = ReadProjectFile("DynamicIslandBar", "DynamicIslandBar.csproj");
        var programCode = ReadProjectFile("DynamicIslandBar", "Program.cs");

        Assert.Contains("<StartupObject>DynamicIslandBar.Program</StartupObject>", projectFile);
        Assert.Contains("TaskbarRestoreWatchdog.TryGetParentProcessId(args", programCode);
        Assert.Contains("TaskbarRestoreWatchdog.TryRestoreTaskbar(args", programCode);
        Assert.Contains("new App()", programCode);
        Assert.True(
            programCode.IndexOf("TaskbarRestoreWatchdog.TryRestoreTaskbar(args", StringComparison.Ordinal) <
            programCode.IndexOf("new App()", StringComparison.Ordinal));
        Assert.True(
            programCode.IndexOf("TaskbarRestoreWatchdog.TryGetParentProcessId(args", StringComparison.Ordinal) <
            programCode.IndexOf("new App()", StringComparison.Ordinal));
    }

    private static string ReadProjectFile(params string[] pathParts)
    {
        return RepositoryFile.Read(pathParts);
    }
}
