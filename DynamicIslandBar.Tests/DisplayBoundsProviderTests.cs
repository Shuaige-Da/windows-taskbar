using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class DisplayBoundsProviderTests
{
    [Fact]
    public void BottomWindowFrame_DocksCapsuleBodyAtTaskbarEdge()
    {
        var metrics = CapsuleLayoutManager.GetMetrics(CapsuleMode.BottomTaskbar, 2048, 1152);

        var frame = CapsuleLayoutManager.GetWindowFrame(
            CapsuleMode.BottomTaskbar,
            metrics,
            screenWidth: 2048,
            screenHeight: 1152);

        var capsuleBottomOffset = ((frame.Height - metrics.CapsuleHeight) / 2) + metrics.CapsuleHeight;
        Assert.Equal(1152, frame.Top + capsuleBottomOffset);
    }
}
