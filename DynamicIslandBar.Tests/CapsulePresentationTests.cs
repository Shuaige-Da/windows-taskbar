using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class CapsulePresentationTests
{
    [Theory]
    [InlineData(CapsuleVisualPart.Chrome, true)]
    [InlineData(CapsuleVisualPart.Dock, true)]
    [InlineData(CapsuleVisualPart.System, true)]
    [InlineData(CapsuleVisualPart.CenterCard, false)]
    [InlineData(CapsuleVisualPart.Lyrics, false)]
    [InlineData(CapsuleVisualPart.Details, false)]
    [InlineData(CapsuleVisualPart.MediaControls, false)]
    public void AutoHideDefault_PreservesExistingCapsuleBehavior(
        CapsuleVisualPart part,
        bool expected)
    {
        Assert.Equal(expected, CapsulePresentationPolicy.GetDefaultAutoHideWithCapsule(part));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void EffectiveVisibility_CombinesPreferenceAndRuntime(
        bool preferredVisible,
        bool runtimeVisible,
        bool expected)
    {
        Assert.Equal(
            expected,
            CapsulePresentationPolicy.IsEffectivelyVisible(preferredVisible, runtimeVisible));
    }

    [Fact]
    public void EffectiveOpacity_MultipliesAutoHideParticipants()
    {
        var opacity = CapsulePresentationPolicy.GetEffectiveOpacity(
            opacityPercent: 80,
            autoHideFactor: 0.1,
            participatesInAutoHide: true);

        Assert.Equal(0.08, opacity, 3);
    }

    [Fact]
    public void EffectiveOpacity_LeavesCenterContentIndependentFromAutoHide()
    {
        var opacity = CapsulePresentationPolicy.GetEffectiveOpacity(
            opacityPercent: 65,
            autoHideFactor: 0.1,
            participatesInAutoHide: false);

        Assert.Equal(0.65, opacity, 3);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(140, 1)]
    public void EffectiveOpacity_ClampsPreferencePercent(int percent, double expected)
    {
        Assert.Equal(
            expected,
            CapsulePresentationPolicy.GetEffectiveOpacity(percent, 1, false),
            3);
    }
}
