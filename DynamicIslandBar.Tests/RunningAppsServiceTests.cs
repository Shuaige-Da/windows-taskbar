using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class RunningAppsServiceTests
{
    [Fact]
    public void BuildSnapshot_GroupsWindowsByNormalizedAppId_AndMarksFavoriteAndHidden()
    {
        var config = new CapsuleConfig();
        CapsuleConfigMutator.SetFavorite(config, @"c:\apps\wechat.exe", true);
        config.HiddenApps.Add(@"c:\apps\qq.exe");

        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("微信", @"c:\apps\wechat.exe", 101, false),
                new WindowAppCandidate("微信聊天", @"c:\apps\wechat.exe", 102, false),
                new WindowAppCandidate("QQ", @"c:\apps\qq.exe", 201, false)
            ],
            config,
            visibleSlots: 2);

        Assert.Equal(2, snapshot.AllApps.Count);
        Assert.True(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\wechat.exe").IsFavorite);
        Assert.True(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\qq.exe").IsHiddenInCapsule);
    }

    [Fact]
    public void BuildSnapshot_PreservesForegroundStateForGroupedWindowApps()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("微信", @"c:\apps\wechat.exe", 101, false),
                new WindowAppCandidate("微信聊天", @"c:\apps\wechat.exe", 102, true),
                new WindowAppCandidate("QQ", @"c:\apps\qq.exe", 201, false)
            ],
            new CapsuleConfig(),
            visibleSlots: 3);

        Assert.True(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\wechat.exe").IsForeground);
        Assert.False(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\qq.exe").IsForeground);
    }

    [Fact]
    public void BuildSnapshot_UsesOverflowFolderWhenVisibleAppsExceedCapacity()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("A", @"a.exe", 1, false),
                new WindowAppCandidate("B", @"b.exe", 2, false),
                new WindowAppCandidate("C", @"c.exe", 3, false)
            ],
            new CapsuleConfig(),
            visibleSlots: 2);

        Assert.Single(snapshot.MainBarApps);
        Assert.Equal(2, snapshot.OverflowApps.Count);
        Assert.True(snapshot.HasOverflowFolder);
    }

    [Fact]
    public void BuildSnapshot_KeepsFavoriteAppsEvenWhenTheyAreNotRunning()
    {
        var config = new CapsuleConfig();
        CapsuleConfigMutator.SetFavorite(config, @"c:\apps\wechat.exe", true);
        CapsuleConfigMutator.SetKnownLaunchPath(config, @"c:\apps\wechat.exe", @"C:\Apps\WeChat.exe");

        var snapshot = RunningAppsSnapshotBuilder.Build([], config, visibleSlots: 3);

        var favorite = Assert.Single(snapshot.AllApps);
        Assert.Equal(@"c:\apps\wechat.exe", favorite.AppId);
        Assert.False(favorite.IsRunning);
        Assert.True(favorite.IsFavorite);
        Assert.Equal(@"C:\Apps\WeChat.exe", favorite.ExePath);
    }
}
