using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace DynamicIslandBar
{
    public static class SystemInfoService
    {
        #region Battery

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte Reserved1;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        public static string GetBatteryInfo()
        {
            if (!GetSystemPowerStatus(out var s)) return "无法读取电池信息";
            var pct = s.BatteryLifePercent > 100 ? 100 : s.BatteryLifePercent;
            var charging = s.ACLineStatus == 1 ? " - 正在充电" : "";
            var timeStr = "";
            if (s.BatteryLifeTime > 0 && s.BatteryLifeTime != 0x7FFFFFFF)
            {
                var ts = TimeSpan.FromSeconds(s.BatteryLifeTime);
                timeStr = $"\n剩余时间: {ts.Hours}小时{ts.Minutes}分钟";
            }
            return $"电量: {pct}%{charging}{timeStr}";
        }

        public static int GetBatteryPercent()
        {
            if (GetSystemPowerStatus(out var s))
                return s.BatteryLifePercent > 100 ? 100 : s.BatteryLifePercent;
            return -1;
        }

        #endregion

        #region WiFi

        public static string GetWifiInfo()
        {
            var ssid = GetWifiSSID();
            var adapter = GetActiveNetworkAdapter();
            var ip = GetLocalIPAddress();

            var result = $"����: {ssid}";
            if (!string.IsNullOrEmpty(adapter))
                result += $"\n������: {adapter}";
            if (!string.IsNullOrEmpty(ip))
                result += $"\nIP ��ַ: {ip}";
            result += $"\n״̬: ������";
            return result;
        }

        private static string GetWifiSSID()
        {
            try
            {
                var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.GetEncoding("gbk")
                };
                using var proc = Process.Start(psi);
                if (proc == null) return "δ֪";
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(2000);

                foreach (var line in output.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("SSID") && !trimmed.Contains("BSSID"))
                    {
                        var idx = trimmed.IndexOf(':');
                        if (idx >= 0)
                            return trimmed[(idx + 1)..].Trim();
                    }
                }
            }
            catch { }

            // Fallback: use network adapter name
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up
                        && ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        return ni.Name;
                }
            }
            catch { }

            return "δ֪";
        }

        private static string GetActiveNetworkAdapter()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up
                        && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        return ni.Description;
                }
            }
            catch { }
            return "";
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    var props = ni.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                            && !System.Net.IPAddress.IsLoopback(addr.Address))
                            return addr.Address.ToString();
                    }
                }
            }
            catch { }
            return "";
        }

        #endregion

        #region Volume

        // COM Interop for IAudioEndpointVolume
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr endpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out int channelCount);
            int SetMasterVolumeLevel(float levelDB, ref Guid eventContext);
            int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            int GetMasterVolumeLevel(out float levelDB);
            int GetMasterVolumeLevelScalar(out float level);
            int SetChannelVolumeLevel(int channel, float levelDB, ref Guid eventContext);
            int SetChannelVolumeLevelScalar(int channel, float level, ref Guid eventContext);
            int GetChannelVolumeLevel(int channel, out float levelDB);
            int GetChannelVolumeLevelScalar(int channel, out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        private static IAudioEndpointVolume? GetVolumeInterface()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                // eRender=0, eMultimedia=1
                enumerator.GetDefaultAudioEndpoint(0, 1, out IntPtr devicePtr);
                var device = (IMMDevice)Marshal.GetObjectForIUnknown(devicePtr);
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(iid, 1, IntPtr.Zero, out object endpoint);
                Marshal.Release(devicePtr);
                return endpoint as IAudioEndpointVolume;
            }
            catch
            {
                return null;
            }
        }

        public static string GetVolumeInfo()
        {
            try
            {
                var vol = GetVolumeInterface();
                if (vol != null)
                {
                    vol.GetMasterVolumeLevelScalar(out float level);
                    vol.GetMute(out bool muted);
                    var pct = (int)(level * 100);
                    var muteStr = muted ? " (�Ѿ���)" : "";
                    var deviceName = GetDefaultAudioDeviceName();
                    var result = $"����: {pct}%{muteStr}";
                    if (!string.IsNullOrEmpty(deviceName))
                        result += $"\n����豸: {deviceName}";
                    return result;
                }
            }
            catch { }
            return "�޷���ȡ������Ϣ";
        }

        public static int GetVolumePercent()
        {
            try
            {
                var vol = GetVolumeInterface();
                if (vol != null)
                {
                    vol.GetMasterVolumeLevelScalar(out float level);
                    return (int)(level * 100);
                }
            }
            catch { }
            return -1;
        }

        private static string GetDefaultAudioDeviceName()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumerator());
                enumerator.GetDefaultAudioEndpoint(0, 1, out IntPtr devicePtr);
                // Use PKEY_Device_FriendlyName via property store
                var device = Marshal.GetObjectForIUnknown(devicePtr);
                // Open property store
                var pps = GetPropertyStore(devicePtr);
                if (pps != null)
                {
                    var name = GetPropertyValue(pps);
                    Marshal.Release(devicePtr);
                    return name ?? "";
                }
                Marshal.Release(devicePtr);
            }
            catch { }
            return "";
        }

        [DllImport("propsys.dll", CharSet = CharSet.Unicode)]
        private static extern int PSGetPropertyKeyFromName(string name, out PropertyKey key);

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out int cProps);
            int GetAt(int iProp, out PropertyKey pkey);
            int GetValue(ref PropertyKey key, out PropVariant pv);
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pszVal;

            public string? GetString()
            {
                return vt == 31 ? Marshal.PtrToStringUni(pszVal) : null;
            }
        }

        [DllImport("ole32.dll")]
        private static extern void PropVariantClear(ref PropVariant pvar);

        private static IPropertyStore? GetPropertyStore(IntPtr devicePtr)
        {
            try
            {
                // IMMDevice::OpenPropertyStore via vtable index 4
                var vtable = Marshal.ReadIntPtr(devicePtr);
                var openPropStore = Marshal.ReadIntPtr(vtable, IntPtr.Size * 4);
                // STGM_READ = 0
                var hr = (int)Marshal.GetDelegateForFunctionPointer<OpenPropertyStoreDelegate>(openPropStore)(
                    devicePtr, 0, out IntPtr ppPropStore);
                if (hr == 0 && ppPropStore != IntPtr.Zero)
                    return (IPropertyStore)Marshal.GetObjectForIUnknown(ppPropStore);
            }
            catch { }
            return null;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int OpenPropertyStoreDelegate(IntPtr self, int accessMode, out IntPtr ppPropStore);

        private static string? GetPropertyValue(IPropertyStore store)
        {
            try
            {
                // PKEY_Device_FriendlyName
                var key = new PropertyKey
                {
                    fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
                    pid = 14
                };
                store.GetValue(ref key, out PropVariant pv);
                var result = pv.GetString();
                PropVariantClear(ref pv);
                return result;
            }
            catch { }
            return null;
        }

        #endregion

        #region Settings Launcher

        public static void OpenWifiSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenSoundSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenBluetoothSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenMobileHotspotSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:network-mobilehotspot") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenNetworkSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenBatterySettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:powersleep") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenTaskManager()
        {
            try { Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true }); }
            catch { }
        }

        public static void OpenLocationPrivacySettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true }); }
            catch { }
        }

        #endregion
    }
}
