using Windows.Devices.Radios;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace DynamicIslandBar;

public enum SystemFeatureToggle
{
    Bluetooth,
    Wifi,
    MobileHotspot,
    Sound
}

public sealed record SystemFeatureToggleState(bool IsOn, bool IsAvailable, string? Message = null);

public static class SystemFeatureToggleService
{
    public static async Task<SystemFeatureToggleState> GetStateAsync(SystemFeatureToggle feature)
    {
        try
        {
            return feature switch
            {
                SystemFeatureToggle.Bluetooth => await GetRadioStateAsync(RadioKind.Bluetooth),
                SystemFeatureToggle.Wifi => await GetRadioStateAsync(RadioKind.WiFi),
                SystemFeatureToggle.MobileHotspot => GetHotspotState(),
                SystemFeatureToggle.Sound => new SystemFeatureToggleState(!AudioService.IsMuted(), true),
                _ => new SystemFeatureToggleState(false, false)
            };
        }
        catch (Exception ex)
        {
            AppDiagnostics.Error($"SystemFeatureState-{feature}", ex);
            return new SystemFeatureToggleState(false, false, "当前设备不支持此开关");
        }
    }

    public static async Task<SystemFeatureToggleState> SetStateAsync(SystemFeatureToggle feature, bool isOn)
    {
        try
        {
            switch (feature)
            {
                case SystemFeatureToggle.Bluetooth:
                    return await SetRadioStateAsync(RadioKind.Bluetooth, isOn);
                case SystemFeatureToggle.Wifi:
                    return await SetRadioStateAsync(RadioKind.WiFi, isOn);
                case SystemFeatureToggle.MobileHotspot:
                    return await SetHotspotStateAsync(isOn);
                case SystemFeatureToggle.Sound:
                    if (AudioService.IsMuted() == isOn)
                    {
                        AudioService.ToggleMute();
                    }
                    return new SystemFeatureToggleState(!AudioService.IsMuted(), true);
                default:
                    return new SystemFeatureToggleState(false, false);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Error($"SystemFeatureToggle-{feature}", ex);
            return new SystemFeatureToggleState(!isOn, false, "系统拒绝了状态切换");
        }
    }

    private static async Task<SystemFeatureToggleState> GetRadioStateAsync(RadioKind kind)
    {
        var access = await Radio.RequestAccessAsync();
        if (access != RadioAccessStatus.Allowed)
        {
            return new SystemFeatureToggleState(false, false, "没有系统无线电控制权限");
        }

        var radio = (await Radio.GetRadiosAsync()).FirstOrDefault(candidate => candidate.Kind == kind);
        return radio is null
            ? new SystemFeatureToggleState(false, false, "未找到对应设备")
            : new SystemFeatureToggleState(radio.State == RadioState.On, true);
    }

    private static async Task<SystemFeatureToggleState> SetRadioStateAsync(RadioKind kind, bool isOn)
    {
        var access = await Radio.RequestAccessAsync();
        if (access != RadioAccessStatus.Allowed)
        {
            return new SystemFeatureToggleState(!isOn, false, "没有系统无线电控制权限");
        }

        var radio = (await Radio.GetRadiosAsync()).FirstOrDefault(candidate => candidate.Kind == kind);
        if (radio is null)
        {
            return new SystemFeatureToggleState(false, false, "未找到对应设备");
        }

        var status = await radio.SetStateAsync(isOn ? RadioState.On : RadioState.Off);
        return new SystemFeatureToggleState(
            status == RadioAccessStatus.Allowed && radio.State == RadioState.On,
            status == RadioAccessStatus.Allowed,
            status == RadioAccessStatus.Allowed ? null : "系统拒绝了状态切换");
    }

    private static SystemFeatureToggleState GetHotspotState()
    {
        var manager = TryCreateTetheringManager();
        return manager is null
            ? new SystemFeatureToggleState(false, false, "当前网络不能共享热点")
            : new SystemFeatureToggleState(
                manager.TetheringOperationalState == TetheringOperationalState.On,
                true);
    }

    private static async Task<SystemFeatureToggleState> SetHotspotStateAsync(bool isOn)
    {
        var manager = TryCreateTetheringManager();
        if (manager is null)
        {
            return new SystemFeatureToggleState(false, false, "当前网络不能共享热点");
        }

        var result = isOn
            ? await manager.StartTetheringAsync()
            : await manager.StopTetheringAsync();
        var succeeded = result.Status == TetheringOperationStatus.Success;
        return new SystemFeatureToggleState(
            succeeded && manager.TetheringOperationalState == TetheringOperationalState.On,
            succeeded,
            succeeded ? null : $"热点切换失败：{result.Status}");
    }

    private static NetworkOperatorTetheringManager? TryCreateTetheringManager()
    {
        var profile = NetworkInformation.GetInternetConnectionProfile();
        return profile is null
            ? null
            : NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
    }
}
