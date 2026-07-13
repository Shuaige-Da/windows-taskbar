using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class SystemInfoServiceTextTests
{
    [Fact]
    public void GetBatteryInfo_ReturnsReadableChineseText()
    {
        var info = SystemInfoService.GetBatteryInfo();

        Assert.DoesNotContain("�", info);
        Assert.True(info.Contains("电量") || info.Contains("无法读取电池信息"));
    }

    [Fact]
    public void GetWifiInfo_ReturnsReadableChineseText()
    {
        var info = SystemInfoService.GetWifiInfo();

        Assert.DoesNotContain("�", info);
        Assert.Contains("网络", info);
        Assert.Contains("状态", info);
    }

    [Fact]
    public void GetVolumeInfo_ReturnsReadableChineseText()
    {
        var info = SystemInfoService.GetVolumeInfo();

        Assert.DoesNotContain("�", info);
        Assert.True(info.Contains("音量") || info.Contains("无法读取音量信息"));
    }
}
