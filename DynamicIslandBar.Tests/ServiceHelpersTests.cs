using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class AudioDeviceListBuilderTests
{
    [Fact]
    public void ImmDeviceCollectionGuid_MatchesWindowsSdkDefinition()
    {
        Assert.Equal(
            "0BD7A1BE-7A1A-44DB-8397-CC5392387B5E",
            AudioInteropConstants.ImmDeviceCollectionGuid);
    }

    [Fact]
    public void Build_RemovesDuplicateIds_AndMarksDefaultDevice()
    {
        var devices = AudioDeviceListBuilder.Build(
            [
                new AudioDevice { Id = "speaker", Name = "Speakers" },
                new AudioDevice { Id = "usb", Name = "USB Audio" },
                new AudioDevice { Id = "speaker", Name = "Speakers" }
            ],
            "usb");

        Assert.Collection(
            devices,
            device =>
            {
                Assert.Equal("usb", device.Id);
                Assert.Equal("USB Audio", device.Name);
                Assert.True(device.IsDefault);
            },
            device =>
            {
                Assert.Equal("speaker", device.Id);
                Assert.Equal("Speakers", device.Name);
                Assert.False(device.IsDefault);
            });
    }
}

public class WifiHelpersTests
{
    [Fact]
    public void ParseSavedProfiles_ExtractsEveryProfileName()
    {
        const string output = """
            Profiles on interface WLAN:

            Group policy profiles (read only)
            ---------------------------------
                <None>

            User profiles
            -------------
                All User Profile     : ZstuWlan
                All User Profile     : rainwave
                All User Profile     : 星际迷航：Warp热点
            """;

        var profiles = WifiTextParser.ParseSavedProfiles(output);

        Assert.Equal(["ZstuWlan", "rainwave", "星际迷航：Warp热点"], profiles);
    }

    [Fact]
    public void ParseCurrentProfileName_ExtractsConnectedProfileFromPowerShellOutput()
    {
        const string output = """
            ZstuWlan
            """;

        var currentProfile = WifiTextParser.ParseCurrentProfileName(output);

        Assert.Equal("ZstuWlan", currentProfile);
    }

    [Fact]
    public void BuildNetworkList_FallsBackToSavedProfiles_AndKeepsConnectedFirst()
    {
        var networks = WifiNetworkListBuilder.Build(
            scannedNetworks: [],
            savedProfiles: ["ZstuWlan", "rainwave"],
            currentSsid: "rainwave");

        Assert.Collection(
            networks,
            network =>
            {
                Assert.Equal("rainwave", network.Ssid);
                Assert.True(network.IsConnected);
                Assert.True(network.HasSavedProfile);
                Assert.Equal("已保存", network.SignalStrength);
            },
            network =>
            {
                Assert.Equal("ZstuWlan", network.Ssid);
                Assert.False(network.IsConnected);
                Assert.True(network.HasSavedProfile);
                Assert.Equal("已保存", network.SignalStrength);
            });
    }

    [Fact]
    public void BuildNetworkList_MarksScannedNetworksWithSavedProfiles()
    {
        var networks = WifiNetworkListBuilder.Build(
            scannedNetworks:
            [
                new WifiNetwork { Ssid = "saved", SignalStrength = "90%", IsSecured = true },
                new WifiNetwork { Ssid = "new", SignalStrength = "80%", IsSecured = true }
            ],
            savedProfiles: ["saved"],
            currentSsid: null);

        Assert.True(networks.Single(network => network.Ssid == "saved").HasSavedProfile);
        Assert.False(networks.Single(network => network.Ssid == "new").HasSavedProfile);
    }

    [Fact]
    public void DetectIssue_ReturnsLocationPermissionRequired_WhenNetshReportsLocationBlock()
    {
        const string output = """
            Network shell commands need location permission to access WLAN information.
            Function WlanQueryInterface returns error 5:
            The requested operation requires elevation (Run as administrator).
            """;

        var issue = WifiAccessAnalyzer.DetectIssue(output);

        Assert.Equal(WifiAccessIssue.LocationPermissionRequired, issue);
    }
}

public class PermissionDecisionEngineTests
{
    [Fact]
    public void Check_RequestsPrompt_WhenPermissionIsUnknown_AndGlobalAllowIsDisabled()
    {
        var state = new PermissionState { AllowAll = false };

        var result = PermissionDecisionEngine.Check(state, AppPermission.WifiNearbyNetworks);

        Assert.False(result.IsGranted);
        Assert.True(result.ShouldPrompt);
    }

    [Fact]
    public void ApplyDecision_AllowCurrent_GrantsOnlyCurrentPermission()
    {
        var state = new PermissionState { AllowAll = false };

        PermissionDecisionEngine.ApplyDecision(state, AppPermission.AudioControl, PermissionDecision.AllowCurrent);

        var audioResult = PermissionDecisionEngine.Check(state, AppPermission.AudioControl);
        var wifiResult = PermissionDecisionEngine.Check(state, AppPermission.WifiNearbyNetworks);

        Assert.True(audioResult.IsGranted);
        Assert.False(audioResult.ShouldPrompt);
        Assert.False(wifiResult.IsGranted);
        Assert.True(wifiResult.ShouldPrompt);
    }

    [Fact]
    public void ApplyDecision_AllowAll_GrantsFuturePermissionsWithoutPrompt()
    {
        var state = new PermissionState { AllowAll = false };

        PermissionDecisionEngine.ApplyDecision(state, AppPermission.RunningApps, PermissionDecision.AllowAll);

        var wifiResult = PermissionDecisionEngine.Check(state, AppPermission.WifiNearbyNetworks);
        var appsResult = PermissionDecisionEngine.Check(state, AppPermission.RunningApps);

        Assert.True(wifiResult.IsGranted);
        Assert.False(wifiResult.ShouldPrompt);
        Assert.True(appsResult.IsGranted);
        Assert.False(appsResult.ShouldPrompt);
    }
}
