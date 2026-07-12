using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class AppNotificationAnimationPolicyTests
{
    [Fact]
    public void ShouldAnimate_ReturnsTrueForFirstNotification()
    {
        var now = DateTime.UtcNow;

        Assert.True(AppNotificationAnimationPolicy.ShouldAnimate(null, now, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ShouldAnimate_ThrottlesRepeatedNotificationWithinMinimumInterval()
    {
        var previous = DateTime.UtcNow;

        Assert.False(AppNotificationAnimationPolicy.ShouldAnimate(
            previous,
            previous.AddMilliseconds(900),
            TimeSpan.FromSeconds(2)));
        Assert.True(AppNotificationAnimationPolicy.ShouldAnimate(
            previous,
            previous.AddSeconds(2),
            TimeSpan.FromSeconds(2)));
    }
}
