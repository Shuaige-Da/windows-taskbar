using System.Windows.Threading;

namespace DynamicIslandBar;

[Flags]
internal enum CapsuleSettingsChangeKind
{
    None = 0,
    Theme = 1,
    Appearance = 2,
    Layout = 4,
    Presentation = 8,
    Lyrics = 16,
    Startup = 32,
    ControlCenterAppearance = 64,
    Input = 128,
    All = Theme | Appearance | Layout | Presentation | Lyrics | Startup | ControlCenterAppearance | Input
}

internal sealed class CapsuleSettingsCoordinator : IDisposable
{
    private readonly CapsuleConfig _config;
    private readonly Action<CapsuleSettingsChangeKind> _applyChange;
    private readonly Action<CapsuleConfig> _saveConfig;
    private readonly DispatcherTimer _saveTimer;
    private bool _hasPendingSave;
    private bool _disposed;

    public CapsuleSettingsCoordinator(
        CapsuleConfig config,
        Action<CapsuleSettingsChangeKind> applyChange,
        Action<CapsuleConfig>? saveConfig = null)
    {
        _config = config;
        _applyChange = applyChange;
        _saveConfig = saveConfig ?? CapsuleConfigService.Save;
        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _saveTimer.Tick += SaveTimer_Tick;
    }

    public CapsuleConfig Config => _config;

    public int CurrentCapsuleLengthPercent =>
        _config.Mode is CapsuleMode.TopIsland or CapsuleMode.LeftDock or CapsuleMode.RightDock
            ? _config.TopDockCapsuleLengthPercent
            : _config.CapsuleLengthPercent;

    public void SetTheme(CapsuleThemePreset preset)
    {
        CapsuleConfigMutator.SetThemePreset(_config, preset);
        NotifyChanged(CapsuleSettingsChangeKind.Theme);
    }

    public void SetStartupDisplayMode(StartupDisplayMode mode)
    {
        CapsuleConfigMutator.SetStartupDisplayMode(_config, mode);
        NotifyChanged(CapsuleSettingsChangeKind.Startup);
    }

    public void SetKeyboardNavigationEnabled(bool isEnabled)
    {
        CapsuleConfigMutator.SetKeyboardNavigationEnabled(_config, isEnabled);
        NotifyChanged(CapsuleSettingsChangeKind.Input);
    }

    public void SetBackgroundImage(string? path)
    {
        CapsuleConfigMutator.SetBackgroundImagePath(_config, path);
        NotifyChanged(CapsuleSettingsChangeKind.Appearance);
    }

    public void SetBackgroundImageOpacity(int percent)
    {
        CapsuleConfigMutator.SetBackgroundImageOpacityPercent(_config, percent);
        NotifyChanged(CapsuleSettingsChangeKind.Appearance);
    }

    public void SetBackgroundImageStretchMode(string mode)
    {
        CapsuleConfigMutator.SetBackgroundImageStretchMode(_config, mode);
        NotifyChanged(CapsuleSettingsChangeKind.Appearance);
    }

    public void SetControlCenterBackgroundImage(string? path)
    {
        CapsuleConfigMutator.SetControlCenterBackgroundImagePath(_config, path);
        NotifyChanged(CapsuleSettingsChangeKind.ControlCenterAppearance);
    }

    public void SetControlCenterBackgroundImageOpacity(int percent)
    {
        CapsuleConfigMutator.SetControlCenterBackgroundImageOpacityPercent(_config, percent);
        NotifyChanged(CapsuleSettingsChangeKind.ControlCenterAppearance);
    }

    public void SetControlCenterBackgroundImageStretchMode(string mode)
    {
        CapsuleConfigMutator.SetControlCenterBackgroundImageStretchMode(_config, mode);
        NotifyChanged(CapsuleSettingsChangeKind.ControlCenterAppearance);
    }

    public void SetControlCenterBackgroundMode(ControlCenterBackgroundMode mode)
    {
        CapsuleConfigMutator.SetControlCenterBackgroundMode(_config, mode);
        NotifyChanged(CapsuleSettingsChangeKind.ControlCenterAppearance);
    }

    public void SetGlassOpacity(int percent) => UpdateAppearance(
        () => CapsuleConfigMutator.SetGlassOpacityPercent(_config, percent));

    public void SetShadow(int percent) => UpdateAppearance(
        () => CapsuleConfigMutator.SetShadowPercent(_config, percent));

    public void SetCapsuleThickness(int percent) => UpdateLayout(
        () => CapsuleConfigMutator.SetCapsuleThicknessPercent(_config, percent));

    public void SetCapsuleLength(int percent) => UpdateLayout(() =>
    {
        if (_config.Mode is CapsuleMode.TopIsland or CapsuleMode.LeftDock or CapsuleMode.RightDock)
        {
            CapsuleConfigMutator.SetTopDockCapsuleLengthPercent(_config, percent);
        }
        else
        {
            CapsuleConfigMutator.SetCapsuleLengthPercent(_config, percent);
        }
    });

    public void SetCenterCardWidth(int percent) => UpdateLayout(
        () => CapsuleConfigMutator.SetCenterCardWidthPercent(_config, percent));

    public void SetGlowIntensity(int percent) => UpdateAppearance(
        () => CapsuleConfigMutator.SetGlowIntensityPercent(_config, percent));

    public void SetGlowThickness(int percent) => UpdateAppearance(
        () => CapsuleConfigMutator.SetGlowThicknessPercent(_config, percent));

    public void SetGlowSpeed(int percent) => UpdateAppearance(
        () => CapsuleConfigMutator.SetGlowSpeedPercent(_config, percent));

    public void SetLyricLanguage(LyricLanguage language)
    {
        CapsuleConfigMutator.SetLyricLanguage(_config, language);
        NotifyChanged(CapsuleSettingsChangeKind.Lyrics);
    }

    public void SetPartVisibility(CapsuleVisualPart part, bool isVisible)
    {
        CapsuleConfigMutator.SetPartVisibility(_config, part, isVisible);
        NotifyChanged(CapsuleSettingsChangeKind.Presentation);
    }

    public void SetPartOpacity(CapsuleVisualPart part, int percent)
    {
        CapsuleConfigMutator.SetPartOpacityPercent(_config, part, percent);
        NotifyChanged(CapsuleSettingsChangeKind.Presentation);
    }

    public void SetPartAutoHideWithCapsule(CapsuleVisualPart part, bool autoHideWithCapsule)
    {
        CapsuleConfigMutator.SetPartAutoHideWithCapsule(_config, part, autoHideWithCapsule);
        NotifyChanged(CapsuleSettingsChangeKind.Presentation);
    }

    public void ReplaceConfiguration(CapsuleConfig replacement)
    {
        Flush();
        CapsuleConfigMutator.ReplaceWith(_config, replacement);
        NotifyChanged(CapsuleSettingsChangeKind.All);
    }

    public void Flush()
    {
        _saveTimer.Stop();
        if (!_hasPendingSave)
        {
            return;
        }

        _saveConfig(_config);
        _hasPendingSave = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Flush();
        _saveTimer.Tick -= SaveTimer_Tick;
    }

    private void UpdateAppearance(Action update)
    {
        update();
        NotifyChanged(CapsuleSettingsChangeKind.Appearance);
    }

    private void UpdateLayout(Action update)
    {
        update();
        NotifyChanged(CapsuleSettingsChangeKind.Layout);
    }

    private void NotifyChanged(CapsuleSettingsChangeKind changeKind)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _applyChange(changeKind);
        _hasPendingSave = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        Flush();
    }
}
