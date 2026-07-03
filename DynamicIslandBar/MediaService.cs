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

        // Timer-based position estimation for apps that don't report position via SMTC
        private string? _estimatedTitle;
        private string? _estimatedArtist;
        private readonly System.Diagnostics.Stopwatch _songTimer = new();
        private bool _wasPlaying;
        private bool _firstSongSeen;

        /// <summary>
        /// Whether at least one song change has been detected via SMTC events.
        /// Lyrics are only synced after the first song change (timer starts at 0 for new songs).
        /// </summary>
        public bool HasSeenSongChange => _firstSongSeen;
        private TimeSpan _realDuration = TimeSpan.Zero;

        // SMTC event-based position tracking
        private TimeSpan _lastSmtcPosition = TimeSpan.Zero;
        private readonly System.Diagnostics.Stopwatch _smtcTimer = new();
        private GlobalSystemMediaTransportControlsSession? _subscribedSession;

        /// <summary>
        /// Set the real duration from external source (e.g. lyrics API) for better position estimation.
        /// </summary>
        public void SetRealDuration(TimeSpan duration)
        {
            _realDuration = duration;
        }

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

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            try
            {
                var props = sender.TryGetMediaPropertiesAsync().AsTask().Result;
                var newTitle = props?.Title;
                var newArtist = props?.Artist;
                if (newTitle != _estimatedTitle || newArtist != _estimatedArtist)
                {
                    _estimatedTitle = newTitle;
                    _estimatedArtist = newArtist;
                    _songTimer.Restart();
                    _firstSongSeen = true;
                    _wasPlaying = true;
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
                    _lastSmtcPosition = pos;
                    _smtcTimer.Restart();
                }
            }
            catch { }
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, object args)
        {
            try
            {
                var status = sender.GetPlaybackInfo()?.PlaybackStatus;
                if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    var props = sender.TryGetMediaPropertiesAsync().AsTask().Result;
                    if (props?.Title != _estimatedTitle || props?.Artist != _estimatedArtist)
                    {
                        _estimatedTitle = props?.Title;
                        _estimatedArtist = props?.Artist;
                        _songTimer.Restart();
                        _firstSongSeen = true;
                        _wasPlaying = true;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Fast position-only read (no SMTC calls). Returns the Stopwatch-estimated position.
        /// Use for high-frequency lyrics updates.
        /// </summary>
        public TimeSpan GetCurrentPosition()
        {
            if (_lastSmtcPosition > TimeSpan.Zero && _smtcTimer.IsRunning)
                return _lastSmtcPosition + _smtcTimer.Elapsed;
            if (_firstSongSeen)
            {
                // Use Elapsed even when timer is stopped (retains last value after pause)
                var pos = _songTimer.Elapsed;
                var maxDur = _realDuration > TimeSpan.Zero ? _realDuration : TimeSpan.Zero;
                if (maxDur > TimeSpan.Zero && pos > maxDur)
                    return maxDur;
                return pos;
            }
            return TimeSpan.Zero;
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

                var duration = endTime;

                // If SMTC timeline position is 0, check if we got position from events
                if (position == TimeSpan.Zero && _lastSmtcPosition > TimeSpan.Zero && _smtcTimer.IsRunning)
                {
                    position = _lastSmtcPosition + _smtcTimer.Elapsed;
                }

                // Timer-based position estimation for apps that don't report position via SMTC
                var title = props?.Title;
                var artist = props?.Artist;
                if (isPlaying && position == TimeSpan.Zero && !string.IsNullOrWhiteSpace(title))
                {
                    if (title != _estimatedTitle || artist != _estimatedArtist)
                    {
                        // Song changed - start fresh timer from 0
                        _estimatedTitle = title;
                        _estimatedArtist = artist;
                        _songTimer.Restart();
                        _wasPlaying = true;
                        _firstSongSeen = true;
                    }
                    else if (!_wasPlaying)
                    {
                        _songTimer.Restart();
                        _wasPlaying = true;
                    }
                    else if (_firstSongSeen)
                    {
                        position = _songTimer.Elapsed;
                        var maxDuration = _realDuration > TimeSpan.Zero ? _realDuration : duration;
                        if (maxDuration > TimeSpan.Zero && position > maxDuration)
                            position = maxDuration;
                    }
                }
                else if (!isPlaying)
                {
                    _wasPlaying = false;
                    _songTimer.Stop();
                }
                else if (isPlaying && position > TimeSpan.Zero)
                {
                    // SMTC is reporting position - sync timer
                    _songTimer.Restart();
                    _songTimer.Stop();
                    // Set the stopwatch elapsed by restarting and immediately stopping won't work
                    // Instead, track the offset
                    _estimatedTitle = title;
                    _estimatedArtist = artist;
                    _wasPlaying = true;
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
                    var procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0) return procs[0].Id;
                }

                var aumidLower = aumid.ToLowerInvariant();
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        var procName = proc.ProcessName.ToLowerInvariant();
                        if (aumidLower.Contains(procName) && procName.Length > 2)
                            return proc.Id;
                    }
                    catch { }
                }

                var musicProcessNames = new[] { "cloudmusic", "qqmusic", "kugou", "kuwo", "spotify", "foobar2000", "MusicBee" };
                foreach (var name in musicProcessNames)
                {
                    var procs = Process.GetProcessesByName(name);
                    if (procs.Length > 0) return procs[0].Id;
                }

                return AudioService.GetActiveAudioSessionPid();
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
