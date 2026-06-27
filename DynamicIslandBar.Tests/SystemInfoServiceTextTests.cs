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
}
