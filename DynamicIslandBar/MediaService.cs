using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.IO;

namespace DynamicIslandBar
{
    /// <summary>
    /// Wraps Windows GlobalSystemMediaTransportControlsSessionManager to provide
    /// media session info, playback state, timeline, and transport controls.
    /// </summary>
    public class MediaService
    {
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private string? _targetSessionAumid;

        public async Task InitializeAsync()
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }

        public void SetTargetSession(string? aumid)
        {
            _targetSessionAumid = aumid;
        }

        private GlobalSystemMediaTransportControlsSession? GetSession()
        {
            if (_manager == null) return null;

            // 1. Try to find by target AUMID (set from snapshot)
            if (!string.IsNullOrWhiteSpace(_targetSessionAumid))
            {
                var sessions = _manager.GetSessions();
                foreach (var s in sessions)
                {
                    try
                    {
                        if (string.Equals(s.SourceAppUserModelId, _targetSessionAumid,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            System.Diagnostics.Debug.WriteLine($"[MediaService] GetSession: found by AUMID match: {_targetSessionAumid}");
                            return s;
                        }
                    }
                    catch { }
                }
                System.Diagnostics.Debug.WriteLine($"[MediaService] GetSession: AUMID match failed for {_targetSessionAumid}, falling back");
            }

            // 2. Try GetCurrentSession
            var current = _manager.GetCurrentSession();
            if (current != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MediaService] GetSession: using GetCurrentSession, AUMID={current.SourceAppUserModelId}");
                return current;
            }

            // 3. Fallback: enumerate and find first session that's playing
            try
            {
                var allSessions = _manager.GetSessions();
                System.Diagnostics.Debug.WriteLine($"[MediaService] GetSession: total sessions={allSessions.Count}");
                foreach (var s in allSessions)
                {
                    try
                    {
                        var info = s.GetPlaybackInfo();
                        if (info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MediaService] GetSession: found playing session, AUMID={s.SourceAppUserModelId}");
                            return s;
                        }
                    }
                    catch { }
                }
                return allSessions.Count > 0 ? allSessions[0] : null;
            }
            catch { return null; }
        }

        public async Task<(string? Title, string? Artist, bool IsPlaying, TimeSpan Position, TimeSpan Duration)> GetMediaInfoAsync()
        {
            var session = GetSession();
            if (session == null)
                return (null, null, false, TimeSpan.Zero, TimeSpan.Zero);

            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var info = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();

                return (
                    props?.Title,
                    props?.Artist,
                    info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    timeline?.Position ?? TimeSpan.Zero,
                    timeline?.EndTime ?? TimeSpan.Zero
                );
            }
            catch
            {
                return (null, null, false, TimeSpan.Zero, TimeSpan.Zero);
            }
        }

        public async Task<byte[]?> GetThumbnailBytesAsync()
        {
            var session = GetSession();
            if (session == null) return null;
            IRandomAccessStreamReference? thumbnail = null;
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                thumbnail = props?.Thumbnail;
            }
            catch { return null; }

            if (thumbnail == null) return null;

            try
            {
                using var stream = await thumbnail.OpenReadAsync();
                using var reader = new DataReader(stream);
                var bytes = new byte[stream.Size];
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);
                return bytes;
            }
            catch { return null; }
        }

        public async Task<bool> PlayAsync()
        {
            var session = GetSession();
            return session != null && await session.TryPlayAsync();
        }

        public async Task<bool> PauseAsync()
        {
            var session = GetSession();
            return session != null && await session.TryPauseAsync();
        }

        public async Task<bool> NextAsync()
        {
            var session = GetSession();
            return session != null && await session.TrySkipNextAsync();
        }

        public async Task<bool> PreviousAsync()
        {
            var session = GetSession();
            return session != null && await session.TrySkipPreviousAsync();
        }

        public async Task<bool> SeekAsync(long positionMilliseconds)
        {
            var session = GetSession();
            return session != null && await session.TryChangePlaybackPositionAsync(positionMilliseconds);
        }

        public int GetMusicAppProcessId()
        {
            try
            {
                var session = GetSession();
                if (session == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MediaService] GetMusicAppProcessId: no session");
                    return 0;
                }

                var aumid = session.SourceAppUserModelId;
                System.Diagnostics.Debug.WriteLine($"[MediaService] GetMusicAppProcessId: AUMID={aumid}");
                if (string.IsNullOrWhiteSpace(aumid)) return 0;

                // Parse AUMID: typically "AppName!UniqueId" or "C:\path\to\exe.exe"
                var appPart = aumid.Contains('!')
                    ? aumid[..aumid.IndexOf('!')]
                    : aumid;

                // Try matching by process name
                var searchNames = new List<string>();
                var fileName = Path.GetFileNameWithoutExtension(appPart);
                if (!string.IsNullOrWhiteSpace(fileName))
                    searchNames.Add(fileName);
                // Also try the full AUMID as-is for some apps
                var aumidFileName = Path.GetFileNameWithoutExtension(aumid);
                if (!string.IsNullOrWhiteSpace(aumidFileName) && !searchNames.Contains(aumidFileName))
                    searchNames.Add(aumidFileName);

                foreach (var name in searchNames)
                {
                    var procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MediaService] PID found by process name '{name}': {procs[0].Id}");
                        return procs[0].Id;
                    }
                }

                // Fallback: match any process whose ProcessName partially matches the AUMID
                var aumidLower = aumid.ToLowerInvariant();
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        var procName = proc.ProcessName.ToLowerInvariant();
                        if (aumidLower.Contains(procName) && procName.Length > 2)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MediaService] PID found by AUMID partial match '{procName}': {proc.Id}");
                            return proc.Id;
                        }
                    }
                    catch { /* Some processes deny access */ }
                }

                // Fallback 2: try common music app process names
                var musicProcessNames = new[] { "cloudmusic", "qqmusic", "kugou", "kuwo", "spotify", "foobar2000", "MusicBee" };
                foreach (var name in musicProcessNames)
                {
                    var procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MediaService] PID found by music app name '{name}': {procs[0].Id}");
                        return procs[0].Id;
                    }
                }

                // Fallback 3: use WASAPI active audio session
                var wasapiPid = AudioService.GetActiveAudioSessionPid();
                System.Diagnostics.Debug.WriteLine($"[MediaService] WASAPI fallback PID: {wasapiPid}");
                return wasapiPid;
            }
            catch { }
            return 0;
        }

        // ─── Playback Mode (Sequential / Loop All / Loop One / Shuffle) ───
        // Uses reflection because GlobalSystemMediaTransportControlsAutoRepeatMode enum
        // may not be directly referenceable in the .NET projection for this TFM.

        private static Type? GetAutoRepeatModeEnumType()
        {
            try
            {
                var sessionType = typeof(GlobalSystemMediaTransportControlsSession);
                var playbackInfoType = sessionType.GetMethod("GetPlaybackInfo")?.ReturnType;
                return playbackInfoType?.GetProperty("AutoRepeatMode")?.PropertyType;
            }
            catch { return null; }
        }

        /// <summary>
        /// 0=Sequential, 1=LoopAll, 2=LoopOne, 3=Shuffle
        /// </summary>
        public int GetPlaybackMode()
        {
            try
            {
                var session = GetSession();
                if (session == null) return 0;

                var info = session.GetPlaybackInfo();
                if (info == null) return 0;

                // Check IsShuffleActive (bool? property)
                var shuffleProp = info.GetType().GetProperty("IsShuffleActive");
                if (shuffleProp?.GetValue(info) is true)
                    return 3;

                // Check AutoRepeatMode (enum property)
                var repeatProp = info.GetType().GetProperty("AutoRepeatMode");
                var repeatVal = repeatProp?.GetValue(info);
                if (repeatVal != null)
                {
                    var repeatInt = Convert.ToInt32(repeatVal);
                    return repeatInt switch { 1 => 1, 2 => 2, _ => 0 };
                }
                return 0;
            }
            catch { return 0; }
        }

        public async Task<bool> SetPlaybackModeAsync(int mode)
        {
            var session = GetSession();
            if (session == null) return false;

            try
            {
                var sessionType = session.GetType();
                var shuffleMethod = sessionType.GetMethod("TryChangeShuffleActiveAsync");
                var repeatMethod = sessionType.GetMethod("TryChangeAutoRepeatModeAsync");
                var enumType = GetAutoRepeatModeEnumType();

                object?[] CreateRepeatArgs(int val) =>
                    enumType != null ? new object[] { Enum.ToObject(enumType, val) } : new object[] { val };

                switch (mode)
                {
                    case 0: // Sequential
                        if (shuffleMethod != null)
                            await (Task<bool>)shuffleMethod.Invoke(session, new object[] { false })!;
                        if (repeatMethod != null)
                            await (Task<bool>)repeatMethod.Invoke(session, CreateRepeatArgs(0))!;
                        break;
                    case 1: // Loop All
                        if (shuffleMethod != null)
                            await (Task<bool>)shuffleMethod.Invoke(session, new object[] { false })!;
                        if (repeatMethod != null)
                            await (Task<bool>)repeatMethod.Invoke(session, CreateRepeatArgs(1))!;
                        break;
                    case 2: // Loop One
                        if (shuffleMethod != null)
                            await (Task<bool>)shuffleMethod.Invoke(session, new object[] { false })!;
                        if (repeatMethod != null)
                            await (Task<bool>)repeatMethod.Invoke(session, CreateRepeatArgs(2))!;
                        break;
                    case 3: // Shuffle
                        if (repeatMethod != null)
                            await (Task<bool>)repeatMethod.Invoke(session, CreateRepeatArgs(0))!;
                        if (shuffleMethod != null)
                            await (Task<bool>)shuffleMethod.Invoke(session, new object[] { true })!;
                        break;
                }
                return true;
            }
            catch { return false; }
        }
    }
}
