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
    public void BuildSnapshot_DoesNotTreatForegroundFloatingWindowAsMainAppForeground()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("微信", @"c:\apps\wechat.exe", 101, false, IsProcessMainWindow: true, WindowArea: 900_000),
                new WindowAppCandidate("悬浮歌词", @"c:\apps\wechat.exe", 102, true, IsToolWindow: true, WindowArea: 40_000),
                new WindowAppCandidate("QQ", @"c:\apps\qq.exe", 201, false)
            ],
            new CapsuleConfig(),
            visibleSlots: 3);

        var wechat = snapshot.AllApps.Single(app => app.AppId == @"c:\apps\wechat.exe");
        Assert.Equal(101, wechat.RepresentativeWindowHandle);
        Assert.Equal("微信", wechat.DisplayName);
        Assert.False(wechat.IsForeground);
        Assert.False(snapshot.AllApps.Single(app => app.AppId == @"c:\apps\qq.exe").IsForeground);
    }

    [Fact]
    public void BuildSnapshot_PrefersLargeNormalWindowWhenMainWindowMetadataIsUnavailable()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("桌宠", @"c:\apps\pet.exe", 101, true, IsToolWindow: true, WindowArea: 32_000),
                new WindowAppCandidate("桌宠设置", @"c:\apps\pet.exe", 102, false, WindowArea: 640_000)
            ],
            new CapsuleConfig(),
            visibleSlots: 3);

        var app = Assert.Single(snapshot.AllApps);
        Assert.Equal(102, app.RepresentativeWindowHandle);
        Assert.Equal("桌宠设置", app.DisplayName);
        Assert.False(app.IsForeground);
    }

    [Fact]
    public void BuildSnapshot_DoesNotPreferProcessMainWindowWhenItIsANonActivatingOverlay()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [
                new WindowAppCandidate("桌面悬浮物", @"c:\apps\pet.exe", 101, true, IsProcessMainWindow: true, IsNoActivateWindow: true, WindowArea: 40_000),
                new WindowAppCandidate("桌宠主页", @"c:\apps\pet.exe", 102, false, WindowArea: 420_000)
            ],
            new CapsuleConfig(),
            visibleSlots: 3);

        var app = Assert.Single(snapshot.AllApps);
        Assert.Equal(102, app.RepresentativeWindowHandle);
        Assert.False(app.IsForeground);
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
        Assert.True(snapshot.ShowAppLibrary);
    }

    [Fact]
    public void BuildSnapshot_AlwaysReservesAppLibrarySlotForSearch()
    {
        var snapshot = RunningAppsSnapshotBuilder.Build(
            [new WindowAppCandidate("A", @"a.exe", 1, false)],
            new CapsuleConfig(),
            visibleSlots: 3);

        Assert.True(snapshot.ShowAppLibrary);
        Assert.Single(snapshot.MainBarApps);
        Assert.Empty(snapshot.OverflowApps);
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
