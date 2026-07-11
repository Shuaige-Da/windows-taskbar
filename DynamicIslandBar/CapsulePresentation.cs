using System.Windows;
using System.Windows.Media.Animation;

namespace DynamicIslandBar;

public enum CapsuleVisualPart
{
    Chrome,
    Dock,
    System,
    Lyrics,
    Details,
    MediaControls,
    CenterCard
}

public sealed class CapsulePartPresentationConfig
{
    public bool IsVisible { get; set; } = true;
    public int OpacityPercent { get; set; } = 100;

    internal CapsulePartPresentationConfig CloneNormalized()
    {
        return new CapsulePartPresentationConfig
        {
            IsVisible = IsVisible,
            OpacityPercent = Math.Clamp(OpacityPercent, 0, 100)
        };
    }
}

public sealed class CapsulePresentationConfig
{
    public CapsulePartPresentationConfig Chrome { get; set; } = new();
    public CapsulePartPresentationConfig Dock { get; set; } = new();
    public CapsulePartPresentationConfig System { get; set; } = new();
    public CapsulePartPresentationConfig Lyrics { get; set; } = new();
    public CapsulePartPresentationConfig Details { get; set; } = new();
    public CapsulePartPresentationConfig MediaControls { get; set; } = new();
    public CapsulePartPresentationConfig CenterCard { get; set; } = new();

    public CapsulePartPresentationConfig Get(CapsuleVisualPart part)
    {
        return part switch
        {
            CapsuleVisualPart.Chrome => Chrome,
            CapsuleVisualPart.Dock => Dock,
            CapsuleVisualPart.System => System,
            CapsuleVisualPart.Lyrics => Lyrics,
            CapsuleVisualPart.Details => Details,
            CapsuleVisualPart.MediaControls => MediaControls,
            CapsuleVisualPart.CenterCard => CenterCard,
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };
    }

    internal CapsulePresentationConfig CloneNormalized()
    {
        return new CapsulePresentationConfig
        {
            Chrome = (Chrome ?? new()).CloneNormalized(),
            Dock = (Dock ?? new()).CloneNormalized(),
            System = (System ?? new()).CloneNormalized(),
            Lyrics = (Lyrics ?? new()).CloneNormalized(),
            Details = (Details ?? new()).CloneNormalized(),
            MediaControls = (MediaControls ?? new()).CloneNormalized(),
            CenterCard = (CenterCard ?? new()).CloneNormalized()
        };
    }
}

public static class CapsulePresentationPolicy
{
    public static bool IsEffectivelyVisible(bool preferredVisible, bool runtimeVisible)
    {
        return preferredVisible && runtimeVisible;
    }

    public static double GetEffectiveOpacity(
        int opacityPercent,
        double autoHideFactor,
        bool participatesInAutoHide)
    {
        var baseOpacity = Math.Clamp(opacityPercent, 0, 100) / 100d;
        var factor = participatesInAutoHide ? Math.Clamp(autoHideFactor, 0, 1) : 1d;
        return baseOpacity * factor;
    }
}

internal sealed class CapsulePresentationController
{
    private static readonly HashSet<CapsuleVisualPart> AutoHideParts =
    [
        CapsuleVisualPart.Chrome,
        CapsuleVisualPart.Dock,
        CapsuleVisualPart.System
    ];

    private readonly IReadOnlyDictionary<CapsuleVisualPart, IReadOnlyList<FrameworkElement>> _targets;
    private readonly Dictionary<CapsuleVisualPart, PartState> _states = [];
    private double _autoHideFactor = 1;

    public CapsulePresentationController(
        IReadOnlyDictionary<CapsuleVisualPart, IReadOnlyList<FrameworkElement>> targets)
    {
        _targets = targets;
        foreach (var part in Enum.GetValues<CapsuleVisualPart>())
        {
            if (!_targets.TryGetValue(part, out var elements) || elements.Count == 0)
            {
                throw new ArgumentException($"Missing presentation target for {part}.", nameof(targets));
            }

            _states[part] = new PartState();
        }
    }

    public double AutoHideFactor => _autoHideFactor;

    public void ApplyPreferences(CapsulePresentationConfig preferences)
    {
        foreach (var part in Enum.GetValues<CapsuleVisualPart>())
        {
            var preference = preferences.Get(part);
            SetPreference(part, preference.IsVisible, preference.OpacityPercent);
        }
    }

    public void SetPreference(CapsuleVisualPart part, bool isVisible, int opacityPercent)
    {
        var state = _states[part];
        state.PreferredVisible = isVisible;
        state.OpacityPercent = Math.Clamp(opacityPercent, 0, 100);
        ApplyPart(part);
    }

    public void SetRuntimeVisibility(CapsuleVisualPart part, bool isVisible)
    {
        var state = _states[part];
        state.RuntimeVisible = isVisible;
        ApplyPart(part);
    }

    public bool IsEffectivelyVisible(CapsuleVisualPart part)
    {
        var state = _states[part];
        return CapsulePresentationPolicy.IsEffectivelyVisible(
            state.PreferredVisible,
            state.RuntimeVisible);
    }

    public void AnimateAutoHideFactor(double targetFactor, TimeSpan duration)
    {
        _autoHideFactor = Math.Clamp(targetFactor, 0, 1);
        foreach (var part in AutoHideParts)
        {
            ApplyPart(part, duration);
        }
    }

    private void ApplyPart(CapsuleVisualPart part, TimeSpan? animationDuration = null)
    {
        var state = _states[part];
        var isVisible = CapsulePresentationPolicy.IsEffectivelyVisible(
            state.PreferredVisible,
            state.RuntimeVisible);
        var opacity = CapsulePresentationPolicy.GetEffectiveOpacity(
            state.OpacityPercent,
            _autoHideFactor,
            AutoHideParts.Contains(part));

        foreach (var target in _targets[part])
        {
            target.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (animationDuration is { } duration)
            {
                target.BeginAnimation(FrameworkElement.OpacityProperty, new DoubleAnimation
                {
                    To = opacity,
                    Duration = duration,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                });
                continue;
            }

            target.BeginAnimation(FrameworkElement.OpacityProperty, null);
            target.Opacity = opacity;
        }
    }

    private sealed class PartState
    {
        public bool PreferredVisible { get; set; } = true;
        public bool RuntimeVisible { get; set; } = true;
        public int OpacityPercent { get; set; } = 100;
    }
}
