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
            var lyric = title; // GSMTC doesn't provide actual lyrics; show title as marquee text

            return new CenterCardMediaSnapshot(
                IsMusicApp: true,
                IsPlaying: isPlaying,
                Title: string.IsNullOrWhiteSpace(title) ? "正在播放" : title,
                Artist: artist,
                Lyric: lyric,
                SourceAppUserModelId: session.SourceAppUserModelId);
        }
        catch
        {
            return null;
        }
    }
}
