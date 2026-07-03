using System.Runtime.InteropServices;

namespace DynamicIslandBar
{
    public class AudioDevice
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsDefault { get; set; }
    }

    public static class AudioService
    {
        private const int ClsCtxAll = 23;
        private const int StgmRead = 0;

        #region CoreAudioInterop

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        private enum EDataFlow
        {
            Render = 0,
            Capture = 1,
            All = 2
        }

        private enum ERole
        {
            Console = 0,
            Multimedia = 1,
            Communications = 2
        }

        [Flags]
        private enum DeviceState
        {
            Active = 0x1,
            Disabled = 0x2,
            NotPresent = 0x4,
            Unplugged = 0x8,
            All = Active | Disabled | NotPresent | Unplugged
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pointerValue;

            public string? GetString()
            {
                return vt == 31 ? Marshal.PtrToStringUni(pointerValue) : null;
            }
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant propVariant);

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection devices);
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IMMDevice device);
            int RegisterEndpointNotificationCallback(IntPtr client);
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport]
        [Guid(AudioInteropConstants.ImmDeviceCollectionGuid)]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            int GetCount(out uint count);
            int Item(uint index, out IMMDevice device);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate([MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId, int classContext, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
            int OpenPropertyStore(int storageAccessMode, out IPropertyStore properties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string deviceId);
            int GetState(out DeviceState state);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out uint propertyCount);
            int GetAt(uint propertyIndex, out PropertyKey key);
            int GetValue(ref PropertyKey key, out PropVariant value);
            int SetValue(ref PropertyKey key, ref PropVariant value);
            int Commit();
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr notify);
            int UnregisterControlChangeNotify(IntPtr notify);
            int GetChannelCount(out int channelCount);
            int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
            int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            int GetMasterVolumeLevel(out float levelDb);
            int GetMasterVolumeLevelScalar(out float level);
            int SetChannelVolumeLevel(int channel, float levelDb, ref Guid eventContext);
            int SetChannelVolumeLevelScalar(int channel, float level, ref Guid eventContext);
            int GetChannelVolumeLevel(int channel, out float levelDb);
            int GetChannelVolumeLevelScalar(int channel, out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        [ComImport]
        [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        private class PolicyConfigClient
        {
        }

        [ComImport]
        [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr formatPointer);
            int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.Bool)] bool defaultFormat, IntPtr formatPointer);
            int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr endpointFormat, IntPtr mixFormat);
            int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.Bool)] bool defaultPeriod, IntPtr defaultPeriodPointer, IntPtr minimumPeriodPointer);
            int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr processingPeriodPointer);
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr shareModePointer);
            int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr shareModePointer);
            int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref PropertyKey key, out PropVariant value);
            int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ref PropertyKey key, ref PropVariant value);
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
            int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, [MarshalAs(UnmanagedType.Bool)] bool isVisible);
        }

        #endregion

        #region AppVolumeInterop

        [ComImport]
        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            int GetAudioSessionControl(ref Guid audioSessionGuid, int streamFlags, out IntPtr sessionControl);
            int GetSimpleAudioVolume(ref Guid audioSessionGuid, int streamFlags, out IntPtr audioVolume);
            int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
            int RegisterSessionNotification(IntPtr sessionNotification);
            int UnregisterSessionNotification(IntPtr sessionNotification);
            int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string streamId, IntPtr duckNotification);
            int UnregisterDuckNotification(IntPtr duckNotification);
        }

        [ComImport]
        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            int GetCount(out int sessionCount);
            int GetSession(int sessionCount, out IAudioSessionControl2 session);
        }

        [ComImport]
        [Guid("B27DD860-5B5E-4B82-9FAA-6D5B6C5F0A14")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            // IAudioSessionControl methods
            int GetState(out int state);
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
            int GetGroupingParam(out Guid param);
            int SetGroupingParam(ref Guid grouping, ref Guid eventContext);
            int RegisterAudioSessionNotification(IntPtr notification);
            int UnregisterAudioSessionNotification(IntPtr notification);
            // IAudioSessionControl2 methods
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            int GetProcessId(out int processId);
            int IsSystemSoundsSession();
            int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }

        [ComImport]
        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            int SetMasterVolume(float level, ref Guid eventContext);
            int GetMasterVolume(out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        private static readonly Guid AudioSessionManager2Guid = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

        private static ISimpleAudioVolume? GetAppVolumeInterface(int processId)
        {
            IMMDeviceEnumerator? enumerator = null;
            IMMDevice? device = null;
            IAudioSessionManager2? sessionManager = null;
            IAudioSessionEnumerator? sessionEnum = null;

            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
                device.Activate(AudioSessionManager2Guid, ClsCtxAll, IntPtr.Zero, out var mgrObj);
                sessionManager = (IAudioSessionManager2)mgrObj;
                sessionManager.GetSessionEnumerator(out sessionEnum);
                sessionEnum.GetCount(out var count);

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2? session = null;
                    try
                    {
                        sessionEnum.GetSession(i, out session);
                        if (session == null) continue;
                        session.GetProcessId(out var pid);
                        if (pid == processId)
                        {
                            var iid = typeof(ISimpleAudioVolume).GUID;
                            var hr = Marshal.QueryInterface(
                                Marshal.GetIUnknownForObject(session), ref iid, out var volumePtr);
                            if (hr == 0 && volumePtr != IntPtr.Zero)
                            {
                                var volume = (ISimpleAudioVolume)Marshal.GetObjectForIUnknown(volumePtr);
                                Marshal.Release(volumePtr);
                                return volume;
                            }
                        }
                    }
                    finally
                    {
                        ReleaseComObject(session);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                ReleaseComObject(sessionEnum);
                ReleaseComObject(sessionManager);
                ReleaseComObject(device);
                ReleaseComObject(enumerator);
            }
        }

        public static int GetAppVolume(int processId)
        {
            var volume = GetAppVolumeInterface(processId);
            if (volume != null)
            {
                try
                {
                    volume.GetMasterVolume(out var level);
                    return (int)Math.Round(level * 100, MidpointRounding.AwayFromZero);
                }
                catch { }
                finally { ReleaseComObject(volume); }
            }
            return -1;
        }

        public static bool SetAppVolume(int processId, int percent)
        {
            var volume = GetAppVolumeInterface(processId);
            if (volume != null)
            {
                try
                {
                    var eventContext = Guid.Empty;
                    volume.SetMasterVolume(Math.Clamp(percent, 0, 100) / 100f, ref eventContext);
                    return true;
                }
                catch { }
                finally { ReleaseComObject(volume); }
            }
            return false;
        }

        /// <summary>
        /// Find the process ID of the first active non-system audio session.
        /// Used as a reliable fallback for finding the music app's PID.
        /// </summary>
        public static int GetActiveAudioSessionPid()
        {
            IMMDeviceEnumerator? enumerator = null;
            IMMDevice? device = null;
            IAudioSessionManager2? sessionManager = null;
            IAudioSessionEnumerator? sessionEnum = null;

            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
                device.Activate(AudioSessionManager2Guid, ClsCtxAll, IntPtr.Zero, out var mgrObj);
                sessionManager = (IAudioSessionManager2)mgrObj;
                sessionManager.GetSessionEnumerator(out sessionEnum);
                sessionEnum.GetCount(out var count);

                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2? session = null;
                    try
                    {
                        sessionEnum.GetSession(i, out session);
                        if (session == null) continue;

                        // Skip system sounds session (returns S_OK=0 if it IS system sounds)
                        var sysResult = session.IsSystemSoundsSession();
                        if (sysResult == 0) continue;

                        session.GetProcessId(out var pid);
                        if (pid > 0)
                            return pid;
                    }
                    finally
                    {
                        ReleaseComObject(session);
                    }
                }
            }
            catch { }
            finally
            {
                ReleaseComObject(sessionEnum);
                ReleaseComObject(sessionManager);
                ReleaseComObject(device);
                ReleaseComObject(enumerator);
            }
            return 0;
        }

        #endregion

        private static readonly PropertyKey DeviceFriendlyNameKey = new()
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = 14
        };

        private static IAudioEndpointVolume? GetVolumeInterface()
        {
            IMMDeviceEnumerator? enumerator = null;
            IMMDevice? device = null;

            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
                var interfaceId = typeof(IAudioEndpointVolume).GUID;
                device.Activate(interfaceId, ClsCtxAll, IntPtr.Zero, out var endpoint);
                return endpoint as IAudioEndpointVolume;
            }
            catch
            {
                return null;
            }
            finally
            {
                ReleaseComObject(device);
                ReleaseComObject(enumerator);
            }
        }

        public static int GetVolume()
        {
            try
            {
                var volume = GetVolumeInterface();
                if (volume != null)
                {
                    volume.GetMasterVolumeLevelScalar(out var level);
                    ReleaseComObject(volume);
                    return (int)Math.Round(level * 100, MidpointRounding.AwayFromZero);
                }
            }
            catch
            {
            }

            return -1;
        }

        public static bool IsMuted()
        {
            try
            {
                var volume = GetVolumeInterface();
                if (volume != null)
                {
                    volume.GetMute(out var muted);
                    ReleaseComObject(volume);
                    return muted;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void SetVolume(int percent)
        {
            try
            {
                var volume = GetVolumeInterface();
                if (volume != null)
                {
                    var eventContext = Guid.Empty;
                    volume.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref eventContext);
                    ReleaseComObject(volume);
                }
            }
            catch
            {
            }
        }

        public static void ToggleMute()
        {
            try
            {
                var volume = GetVolumeInterface();
                if (volume != null)
                {
                    volume.GetMute(out var muted);
                    var eventContext = Guid.Empty;
                    volume.SetMute(!muted, ref eventContext);
                    ReleaseComObject(volume);
                }
            }
            catch
            {
            }
        }

        public static List<AudioDevice> GetOutputDevices()
        {
            IMMDeviceEnumerator? enumerator = null;
            IMMDeviceCollection? collection = null;
            IMMDevice? defaultDevice = null;
            var rawDevices = new List<AudioDevice>();

            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out defaultDevice);
                defaultDevice.GetId(out var defaultDeviceId);

                enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceState.Active, out collection);
                collection.GetCount(out var count);

                for (uint index = 0; index < count; index++)
                {
                    IMMDevice? device = null;
                    try
                    {
                        collection.Item(index, out device);
                        if (device == null)
                        {
                            continue;
                        }

                        device.GetId(out var deviceId);
                        rawDevices.Add(new AudioDevice
                        {
                            Id = deviceId,
                            Name = GetFriendlyName(device)
                        });
                    }
                    finally
                    {
                        ReleaseComObject(device);
                    }
                }

                var devices = AudioDeviceListBuilder.Build(rawDevices, defaultDeviceId);
                if (devices.Count > 0)
                {
                    return devices;
                }
            }
            catch
            {
            }
            finally
            {
                ReleaseComObject(defaultDevice);
                ReleaseComObject(collection);
                ReleaseComObject(enumerator);
            }

            return
            [
                new AudioDevice { Id = "default", Name = "默认输出设备", IsDefault = true }
            ];
        }

        public static bool SwitchDevice(string deviceId)
        {
            IPolicyConfig? policyConfig = null;

            try
            {
                policyConfig = (IPolicyConfig)new PolicyConfigClient();
                policyConfig.SetDefaultEndpoint(deviceId, ERole.Console);
                policyConfig.SetDefaultEndpoint(deviceId, ERole.Multimedia);
                policyConfig.SetDefaultEndpoint(deviceId, ERole.Communications);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseComObject(policyConfig);
            }
        }

        private static string GetFriendlyName(IMMDevice device)
        {
            IPropertyStore? properties = null;

            try
            {
                device.OpenPropertyStore(StgmRead, out properties);
                var propertyKey = DeviceFriendlyNameKey;
                properties.GetValue(ref propertyKey, out var propValue);
                var name = propValue.GetString();
                PropVariantClear(ref propValue);
                return string.IsNullOrWhiteSpace(name) ? "未命名输出设备" : name;
            }
            catch
            {
                return "未命名输出设备";
            }
            finally
            {
                ReleaseComObject(properties);
            }
        }

        private static void ReleaseComObject(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }
    }
}
