using Windows.Media.Control;

namespace DynamicIslandBar;

public interface ICenterCardMediaSnapshotSource
{
    Task<CenterCardMediaSnapshot?> TryGetSnapshotAsync(RunningAppEntry app, CancellationToken cancellationToken = default);
}

public sealed class WindowsMediaSessionSnapshotSource : ICenterCardMediaSnapshotSource
{
    public async Task<CenterCardMediaSnapshot?> TryGetSnapshotAsync(RunningAppEntry app, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                return null;
            }

            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var session = manager.GetSessions()
                .FirstOrDefault(candidate => CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, candidate.SourceAppUserModelId));

            session ??= CenterCardMediaSnapshotProvider.IsLikelyMusicApp(app)
                ? manager.GetCurrentSession()
                : null;
            if (session == null)
            {
                return null;
            }

            if (!CenterCardMediaSnapshotProvider.SourceLooksLikeApp(app, session.SourceAppUserModelId)
                && !CenterCardMediaSnapshotProvider.IsLikelyMusicApp(app))
            {
                return null;
            }

            var properties = await session.TryGetMediaPropertiesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var title = properties.Title?.Trim() ?? string.Empty;
            var artist = properties.Artist?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
            {
                return null;
            }

            var playbackInfo = session.GetPlaybackInfo();
            var isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var timeline = session.GetTimelineProperties();
            var duration = GetDuration(timeline.StartTime, timeline.EndTime);
            var position = GetPosition(timeline.Position, timeline.StartTime, duration);
            WriteDiagnostics(session.SourceAppUserModelId, title, artist, timeline.StartTime, timeline.EndTime, timeline.Position, position, duration);

            return new CenterCardMediaSnapshot(
                IsMusicApp: true,
                IsPlaying: isPlaying,
                Title: string.IsNullOrWhiteSpace(title) ? "正在播放" : title,
                Artist: artist,
                Lyric: string.Empty,
                SourceAppUserModelId: session.SourceAppUserModelId,
                Position: position,
                Duration: duration);
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? GetDuration(TimeSpan startTime, TimeSpan endTime)
    {
        if (endTime <= TimeSpan.Zero)
        {
            return null;
        }

        var duration = endTime > startTime
            ? endTime - startTime
            : endTime;
        return duration > TimeSpan.Zero ? duration : null;
    }

    private static TimeSpan? GetPosition(TimeSpan position, TimeSpan startTime, TimeSpan? duration)
    {
        if (duration is not { } validDuration || position < TimeSpan.Zero)
        {
            return null;
        }

        var relativePosition = startTime > TimeSpan.Zero && position >= startTime
            ? position - startTime
            : position;
        return TimeSpan.FromMilliseconds(Math.Clamp(
            relativePosition.TotalMilliseconds,
            0,
            validDuration.TotalMilliseconds));
    }

    private static void WriteDiagnostics(
        string sourceAppUserModelId,
        string title,
        string artist,
        TimeSpan startTime,
        TimeSpan endTime,
        TimeSpan rawPosition,
        TimeSpan? position,
        TimeSpan? duration)
    {
        var diagnosticsPath = Environment.GetEnvironmentVariable("DYNAMIC_ISLAND_MEDIA_DIAGNOSTICS");
        if (string.IsNullOrWhiteSpace(diagnosticsPath))
        {
            return;
        }

        try
        {
            var line = string.Join(
                " | ",
                DateTimeOffset.Now.ToString("O"),
                sourceAppUserModelId,
                title,
                artist,
                $"start={startTime}",
                $"end={endTime}",
                $"raw={rawPosition}",
                $"position={position?.ToString() ?? "<null>"}",
                $"duration={duration?.ToString() ?? "<null>"}");
            System.IO.File.AppendAllText(diagnosticsPath, line + Environment.NewLine);
        }
        catch
        {
            // Diagnostics are best-effort only.
        }
    }
}
