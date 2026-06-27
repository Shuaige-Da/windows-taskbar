using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class AppPrimaryActionResolverTests
{
    [Fact]
    public void Resolve_ReturnsMinimize_WhenAppWasForegroundInLatestSnapshot()
    {
        var action = AppPrimaryActionResolver.Resolve(
            new RunningAppEntry(
                AppId: "wechat",
                DisplayName: "微信",
                ExePath: @"C:\Apps\WeChat.exe",
                IsRunning: true,
                IsFavorite: false,
                IsHiddenInCapsule: false,
                RepresentativeWindowHandle: 101,
                RepresentativeProcessId: 1234,
                IsForeground: true),
            recentlyActivatedAppId: null);

        Assert.Equal(AppPrimaryAction.Minimize, action);
    }

    [Fact]
    public void Resolve_ReturnsMinimize_WhenAppWasJustActivatedFromCapsule()
    {
        var action = AppPrimaryActionResolver.Resolve(
            new RunningAppEntry(
                AppId: "wechat",
                DisplayName: "微信",
                ExePath: @"C:\Apps\WeChat.exe",
                IsRunning: true,
                IsFavorite: false,
                IsHiddenInCapsule: false,
                RepresentativeWindowHandle: 101,
                RepresentativeProcessId: 1234,
                IsForeground: false),
            recentlyActivatedAppId: "wechat");

        Assert.Equal(AppPrimaryAction.Minimize, action);
    }

    [Fact]
    public void Resolve_ReturnsActivate_WhenRunningAppIsNotForeground()
    {
        var action = AppPrimaryActionResolver.Resolve(
            new RunningAppEntry(
                AppId: "wechat",
                DisplayName: "微信",
                ExePath: @"C:\Apps\WeChat.exe",
                IsRunning: true,
                IsFavorite: false,
                IsHiddenInCapsule: false,
                RepresentativeWindowHandle: 101,
                RepresentativeProcessId: 1234,
                IsForeground: false),
            recentlyActivatedAppId: null);

        Assert.Equal(AppPrimaryAction.ActivateOrLaunch, action);
    }
}
