using Windows.Media.Control;
using System.Diagnostics;

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
                return null;

            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var sessions = manager.GetSessions();

            // Strict match: AUMID must contain app's exe name or app name
            GlobalSystemMediaTransportControlsSession? session = null;
            var exeName = !string.IsNullOrWhiteSpace(app.ExePath)
                ? System.IO.Path.GetFileNameWithoutExtension(app.ExePath)?.ToLowerInvariant() ?? ""
                : "";

            foreach (var candidate in sessions)
            {
                try
                {
                    var aumid = candidate.SourceAppUserModelId?.ToLowerInvariant() ?? "";
                    // Match if AUMID contains the exe name (e.g. "cloudmusic" in "CloudMusic!xxx")
                    if (!string.IsNullOrWhiteSpace(exeName) && exeName.Length > 3 && aumid.Contains(exeName))
                    {
                        session = candidate;
                        Debug.WriteLine($"[SnapshotSource] Matched by exe name: '{exeName}' in '{aumid}'");
                        break;
                    }
                    // Match if AUMID equals app's AppId
                    if (!string.IsNullOrWhiteSpace(app.AppId) &&
                        string.Equals(aumid, app.AppId.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    {
                        session = candidate;
                        Debug.WriteLine($"[SnapshotSource] Matched by AppId: '{app.AppId}'");
                        break;
                    }
                }
                catch { }
            }

            if (session == null)
                return null;

            var properties = await session.TryGetMediaPropertiesAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var title = properties.Title?.Trim() ?? string.Empty;
            var artist = properties.Artist?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
                return null;

            var playbackInfo = session.GetPlaybackInfo();
            var isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            Debug.WriteLine($"[SnapshotSource] OK: Title='{title}', Artist='{artist}', Playing={isPlaying}, AUMID={session.SourceAppUserModelId}");

            return new CenterCardMediaSnapshot(
                IsMusicApp: true,
                IsPlaying: isPlaying,
                Title: string.IsNullOrWhiteSpace(title) ? "正在播放" : title,
                Artist: artist,
                Lyric: string.Empty,
                SourceAppUserModelId: session.SourceAppUserModelId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SnapshotSource] Error: {ex.Message}");
            return null;
        }
    }
}
