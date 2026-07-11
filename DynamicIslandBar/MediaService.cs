using Windows.Media.Control;
using Windows.Storage.Streams;
using Windows.Foundation;
using System.Diagnostics;
using System.IO;

namespace DynamicIslandBar
{
    /// <summary>
    /// Wraps Windows GlobalSystemMediaTransportControlsSessionManager to provide
    /// media session info, playback state, timeline, and transport controls.
    /// </summary>
    public class MediaService : IDisposable
    {
        private static readonly string[] KnownMusicProcessNames =
            ["cloudmusic", "qqmusic", "kugou", "kuwo", "spotify", "foobar2000", "MusicBee"];
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private string? _targetSessionAumid;

        // Timer-based position estimation for apps that don't report position via SMTC
        private string? _estimatedTitle;
        private string? _estimatedArtist;
        private readonly System.Diagnostics.Stopwatch _songTimer = new();
        private TimeSpan _fallbackBasePosition = TimeSpan.Zero;
        private bool _wasPlaying;
        private readonly object _positionSync = new();
        private string? _pendingTitle;
        private string? _pendingArtist;
        private long _pendingIdentityTimestamp;
        private const int IdentityDebounceMilliseconds = 500;

        private TimeSpan _realDuration = TimeSpan.Zero;

        // SMTC event-based position tracking
        private TimeSpan _lastSmtcPosition = TimeSpan.Zero;
        private readonly System.Diagnostics.Stopwatch _smtcTimer = new();
        private GlobalSystemMediaTransportControlsSession? _subscribedSession;
        private bool _disposed;

        /// <summary>
        /// Set the real duration from external source (e.g. lyrics API) for better position estimation.
        /// </summary>
        public void SetRealDuration(TimeSpan duration)
        {
            lock (_positionSync)
            {
                _realDuration = duration;
            }
        }

        public async Task InitializeAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }

        public void SetTargetSession(string? aumid)
        {
            _targetSessionAumid = aumid;
        }

        private GlobalSystemMediaTransportControlsSession? GetSession()
        {
            if (_manager == null) return null;

            // Try to find by target AUMID (set from snapshot)
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
                            return s;
                        }
                    }
                    catch { }
                }
            }

            // Fallback: return current session or first playing session
            var current = _manager.GetCurrentSession();
            if (current != null) return current;

            try
            {
                foreach (var s in _manager.GetSessions())
                {
                    try
                    {
                        if (s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                            return s;
                    }
                    catch { }
                }
                var all = _manager.GetSessions();
                return all.Count > 0 ? all[0] : null;
            }
            catch { return null; }
        }

        private void SubscribeToSessionEvents(GlobalSystemMediaTransportControlsSession session)
        {
            if (_disposed) return;
            if (_subscribedSession == session) return;
            try
            {
                if (_subscribedSession != null)
                {
                    _subscribedSession.TimelinePropertiesChanged -= OnTimelineChanged;
                    _subscribedSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _subscribedSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                }

                _subscribedSession = session;
                session.TimelinePropertiesChanged += OnTimelineChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            }
            catch { }
        }

        private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            try
            {
                var props = await sender.TryGetMediaPropertiesAsync();
                var newTitle = props?.Title;
                var newArtist = props?.Artist;
                var isPlaying = sender.GetPlaybackInfo()?.PlaybackStatus
                    == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                ObserveMediaIdentity(newTitle, newArtist, isPlaying, forceCommit: false);

                await Task.Delay(IdentityDebounceMilliseconds);
                props = await sender.TryGetMediaPropertiesAsync();
                if (string.Equals(props?.Title, newTitle, StringComparison.Ordinal)
                    && string.Equals(props?.Artist, newArtist, StringComparison.Ordinal))
                {
                    ObserveMediaIdentity(newTitle, newArtist, isPlaying, forceCommit: true);
                }
            }
            catch { }
        }

        private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            try
            {
                var pos = sender.GetTimelineProperties()?.Position ?? TimeSpan.Zero;
                if (pos > TimeSpan.Zero)
                {
                    var isPlaying = sender.GetPlaybackInfo()?.PlaybackStatus
                        == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    lock (_positionSync)
                    {
                        SetSmtcAnchorLocked(pos, isPlaying);
                        SetFallbackAnchorLocked(pos, isPlaying);
                    }
                }
            }
            catch { }
        }

        private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            try
            {
                var status = sender.GetPlaybackInfo()?.PlaybackStatus;
                var isPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                lock (_positionSync)
                {
                    UpdatePlaybackStateLocked(isPlaying);
                }

                var props = await sender.TryGetMediaPropertiesAsync();
                ObserveMediaIdentity(props?.Title, props?.Artist, isPlaying, forceCommit: false);
            }
            catch { }
        }

        private bool ObserveMediaIdentity(string? title, string? artist, bool isPlaying, bool forceCommit)
        {
            lock (_positionSync)
            {
                if (string.Equals(title, _estimatedTitle, StringComparison.Ordinal)
                    && string.Equals(artist, _estimatedArtist, StringComparison.Ordinal))
                {
                    _pendingTitle = null;
                    _pendingArtist = null;
                    _pendingIdentityTimestamp = 0;
                    UpdatePlaybackStateLocked(isPlaying);
                    return false;
                }

                var now = Stopwatch.GetTimestamp();
                if (!string.Equals(title, _pendingTitle, StringComparison.Ordinal)
                    || !string.Equals(artist, _pendingArtist, StringComparison.Ordinal))
                {
                    _pendingTitle = title;
                    _pendingArtist = artist;
                    _pendingIdentityTimestamp = now;
                    return false;
                }

                var elapsedMilliseconds = (now - _pendingIdentityTimestamp) * 1000d / Stopwatch.Frequency;
                if (!forceCommit && elapsedMilliseconds < IdentityDebounceMilliseconds)
                {
                    return false;
                }

                _estimatedTitle = title;
                _estimatedArtist = artist;
                _pendingTitle = null;
                _pendingArtist = null;
                _pendingIdentityTimestamp = 0;
                _lastSmtcPosition = TimeSpan.Zero;
                _smtcTimer.Reset();
                _fallbackBasePosition = TimeSpan.Zero;
                _songTimer.Reset();
                _realDuration = TimeSpan.Zero;
                _wasPlaying = isPlaying;
                if (isPlaying)
                {
                    _songTimer.Start();
                }

                return true;
            }
        }

        private void UpdatePlaybackStateLocked(bool isPlaying)
        {
            if (isPlaying == _wasPlaying)
            {
                return;
            }

            if (isPlaying)
            {
                if (_lastSmtcPosition > TimeSpan.Zero)
                {
                    _smtcTimer.Start();
                }
                _songTimer.Start();
            }
            else
            {
                if (_smtcTimer.IsRunning)
                {
                    _lastSmtcPosition += _smtcTimer.Elapsed;
                    _smtcTimer.Reset();
                }
                if (_songTimer.IsRunning)
                {
                    _fallbackBasePosition += _songTimer.Elapsed;
                    _songTimer.Reset();
                }
            }

            _wasPlaying = isPlaying;
        }

        private void SetSmtcAnchorLocked(TimeSpan position, bool isPlaying)
        {
            _lastSmtcPosition = position;
            _smtcTimer.Restart();
            if (!isPlaying)
            {
                _smtcTimer.Stop();
            }
        }

        private void SetFallbackAnchorLocked(TimeSpan position, bool isPlaying)
        {
            _fallbackBasePosition = position;
            _songTimer.Restart();
            if (!isPlaying)
            {
                _songTimer.Stop();
            }
            _wasPlaying = isPlaying;
        }

        /// <summary>
        /// Fast position-only read (no SMTC calls). Returns the Stopwatch-estimated position.
        /// Use for high-frequency lyrics updates.
        /// </summary>
        public TimeSpan GetCurrentPosition()
        {
            lock (_positionSync)
            {
                return GetCurrentPositionLocked();
            }
        }

        private TimeSpan GetCurrentPositionLocked()
        {
            var position = _lastSmtcPosition > TimeSpan.Zero
                ? _lastSmtcPosition + _smtcTimer.Elapsed
                : _fallbackBasePosition + _songTimer.Elapsed;
            if (_realDuration > TimeSpan.Zero && position > _realDuration)
            {
                return _realDuration;
            }
            return position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }

        public async Task<(string? Title, string? Artist, bool IsPlaying, TimeSpan Position, TimeSpan Duration)> GetMediaInfoAsync()
        {
            var session = GetSession();
            if (session == null)
                return (null, null, false, TimeSpan.Zero, TimeSpan.Zero);

            // Subscribe to SMTC events for position tracking
            SubscribeToSessionEvents(session);

            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                var info = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();

                var position = timeline?.Position ?? TimeSpan.Zero;
                var endTime = timeline?.EndTime ?? TimeSpan.Zero;
                var isPlaying = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var title = props?.Title;
                var artist = props?.Artist;
                ObserveMediaIdentity(title, artist, isPlaying, forceCommit: false);

                TimeSpan duration;
                lock (_positionSync)
                {
                    duration = endTime > TimeSpan.Zero ? endTime : _realDuration;
                    if (position > TimeSpan.Zero)
                    {
                        if (_lastSmtcPosition <= TimeSpan.Zero
                            || Math.Abs((position - _lastSmtcPosition).TotalMilliseconds) >= 50)
                        {
                            SetSmtcAnchorLocked(position, isPlaying);
                            SetFallbackAnchorLocked(position, isPlaying);
                        }
                        else
                        {
                            UpdatePlaybackStateLocked(isPlaying);
                        }
                        position = GetCurrentPositionLocked();
                    }
                    else
                    {
                        UpdatePlaybackStateLocked(isPlaying);
                        position = GetCurrentPositionLocked();
                    }
                }

                return (title, artist, isPlaying, position, duration);
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
                if (session == null) return 0;

                var aumid = session.SourceAppUserModelId;
                if (string.IsNullOrWhiteSpace(aumid)) return 0;

                var appPart = aumid.Contains('!') ? aumid[..aumid.IndexOf('!')] : aumid;
                var searchNames = new List<string>();
                var fileName = Path.GetFileNameWithoutExtension(appPart);
                if (!string.IsNullOrWhiteSpace(fileName)) searchNames.Add(fileName);
                var aumidFileName = Path.GetFileNameWithoutExtension(aumid);
                if (!string.IsNullOrWhiteSpace(aumidFileName) && !searchNames.Contains(aumidFileName))
                    searchNames.Add(aumidFileName);

                foreach (var name in searchNames)
                {
                    var processId = FindFirstProcessIdByName(name);
                    if (processId > 0) return processId;
                }

                var aumidLower = aumid.ToLowerInvariant();
                var matchingProcessId = FindFirstProcessId(processName =>
                    processName.Length > 2 && aumidLower.Contains(processName));
                if (matchingProcessId > 0)
                {
                    return matchingProcessId;
                }

                foreach (var name in KnownMusicProcessNames)
                {
                    var processId = FindFirstProcessIdByName(name);
                    if (processId > 0) return processId;
                }

                return AudioService.GetActiveAudioSessionPid();
            }
            catch { }
            return 0;
        }

        private static int FindFirstProcessIdByName(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0 ? processes[0].Id : 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static int FindFirstProcessId(Func<string, bool> predicate)
        {
            var processes = Process.GetProcesses();
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        var processName = process.ProcessName.ToLowerInvariant();
                        if (predicate(processName))
                        {
                            return process.Id;
                        }
                    }
                    catch
                    {
                        // A process can exit or become inaccessible during enumeration.
                    }
                }

                return 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
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
                    return repeatInt switch { 2 => 1, 1 => 2, _ => 0 };
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
                var changed = false;

                object?[] CreateRepeatArgs(int val) =>
                    enumType != null ? new object[] { Enum.ToObject(enumType, val) } : new object[] { val };

                switch (mode)
                {
                    case 0: // Sequential
                        if (shuffleMethod != null)
                            changed |= await AwaitBooleanAsync(shuffleMethod.Invoke(session, new object[] { false }));
                        if (repeatMethod != null)
                            changed |= await AwaitBooleanAsync(repeatMethod.Invoke(session, CreateRepeatArgs(0)));
                        break;
                    case 1: // Loop All
                        if (shuffleMethod != null)
                            changed |= await AwaitBooleanAsync(shuffleMethod.Invoke(session, new object[] { false }));
                        if (repeatMethod != null)
                            changed |= await AwaitBooleanAsync(repeatMethod.Invoke(session, CreateRepeatArgs(2)));
                        break;
                    case 2: // Loop One
                        if (shuffleMethod != null)
                            changed |= await AwaitBooleanAsync(shuffleMethod.Invoke(session, new object[] { false }));
                        if (repeatMethod != null)
                            changed |= await AwaitBooleanAsync(repeatMethod.Invoke(session, CreateRepeatArgs(1)));
                        break;
                    case 3: // Shuffle
                        if (repeatMethod != null)
                            changed |= await AwaitBooleanAsync(repeatMethod.Invoke(session, CreateRepeatArgs(0)));
                        if (shuffleMethod != null)
                            changed |= await AwaitBooleanAsync(shuffleMethod.Invoke(session, new object[] { true }));
                        break;
                }
                return changed;
            }
            catch { return false; }
        }

        private static async Task<bool> AwaitBooleanAsync(object? operation)
        {
            switch (operation)
            {
                case null:
                    return false;
                case Task<bool> task:
                    return await task;
                case IAsyncOperation<bool> asyncOperation:
                    return await asyncOperation.AsTask();
                case IAsyncAction asyncAction:
                    await asyncAction.AsTask();
                    return true;
                case bool result:
                    return result;
                default:
                    return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (_subscribedSession != null)
                {
                    _subscribedSession.TimelinePropertiesChanged -= OnTimelineChanged;
                    _subscribedSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _subscribedSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                }
            }
            catch
            {
                // The WinRT session may already have been torn down.
            }

            _subscribedSession = null;
            _manager = null;
            _targetSessionAumid = null;
            lock (_positionSync)
            {
                _songTimer.Reset();
                _smtcTimer.Reset();
                _pendingTitle = null;
                _pendingArtist = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
