using System.IO;

namespace DynamicIslandBar;

public enum CenterCardDisplayMode
{
    Hidden,
    AppDetails,
    MusicLyricsMarquee,
    MusicDetails
}

public sealed record CenterCardMediaSnapshot(
    bool IsMusicApp,
    bool IsPlaying,
    string Title,
    string Artist,
    string Lyric,
    string? SourceAppUserModelId = null,
    TimeSpan? Position = null,
    TimeSpan? Duration = null);

public sealed record CenterCardPresentation(
    CenterCardDisplayMode Mode,
    string PrimaryText,
    string SecondaryText,
    bool ShowLyricsMarquee,
    bool ShowTransportControls,
    bool ShowAppActions,
    double ProgressRatio = 0,
    string ProgressText = "");

public static class CenterCardPresentationPolicy
{
    public static CenterCardPresentation Build(
        RunningAppEntry? app,
        string status,
        CenterCardMediaSnapshot? media,
        bool isHovered)
    {
        if (app == null)
        {
            return new CenterCardPresentation(
                CenterCardDisplayMode.Hidden,
                string.Empty,
                string.Empty,
                ShowLyricsMarquee: false,
                ShowTransportControls: false,
                ShowAppActions: false);
        }

        if (media is { IsMusicApp: true })
        {
            var (progressRatio, progressText) = BuildProgress(media);

            if (!isHovered && media.IsPlaying)
            {
                return new CenterCardPresentation(
                    CenterCardDisplayMode.MusicLyricsMarquee,
                    string.IsNullOrWhiteSpace(media.Lyric) ? $"{media.Title} - {media.Artist}" : media.Lyric,
                    string.Empty,
                    ShowLyricsMarquee: true,
                    ShowTransportControls: false,
                    ShowAppActions: false);
            }

            return new CenterCardPresentation(
                CenterCardDisplayMode.MusicDetails,
                $"{media.Title} - {media.Artist}",
                media.IsPlaying
                    ? (string.IsNullOrWhiteSpace(media.Lyric) ? "正在播放" : $"歌词：{media.Lyric}")
                    : "已暂停",
                    ShowLyricsMarquee: false,
                    ShowTransportControls: true,
                    ShowAppActions: false,
                    ProgressRatio: progressRatio,
                    ProgressText: progressText);
        }

        return new CenterCardPresentation(
            CenterCardDisplayMode.AppDetails,
            app.DisplayName,
            $"{status} · 单击激活 / 再次单击最小化",
            ShowLyricsMarquee: false,
            ShowTransportControls: false,
            ShowAppActions: true);
    }

    private static (double Ratio, string Text) BuildProgress(CenterCardMediaSnapshot media)
    {
        if (media.Position is not { } position || media.Duration is not { } duration || duration <= TimeSpan.Zero)
        {
            return (0, string.Empty);
        }

        var clampedPosition = TimeSpan.FromMilliseconds(Math.Clamp(
            position.TotalMilliseconds,
            0,
            duration.TotalMilliseconds));
        var ratio = clampedPosition.TotalMilliseconds / duration.TotalMilliseconds;
        return (ratio, $"{FormatTime(clampedPosition)} / {FormatTime(duration)}");
    }

    private static string FormatTime(TimeSpan value)
    {
        var totalSeconds = Math.Max(0, (int)Math.Floor(value.TotalSeconds));
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}

public static class CenterCardMediaSnapshotProvider
{
    private static readonly string[] MusicAppMarkers =
    [
        "music",
        "cloudmusic",
        "qqmusic",
        "kugou",
        "kuwo",
        "spotify",
        "audacity",
        "vlc",
        "potplayer",
        "foobar2000",
        "aimp",
        "wmplayer",
        "mediaplayer",
        "zunemusic",
        "media",
        "网易云",
        "qq音乐",
        "酷狗",
        "酷我"
    ];

    public static CenterCardMediaSnapshot? Resolve(RunningAppEntry app, CenterCardMediaSnapshot? liveSnapshot)
    {
        if (liveSnapshot != null
            && (string.IsNullOrWhiteSpace(liveSnapshot.SourceAppUserModelId)
                || SourceLooksLikeApp(app, liveSnapshot.SourceAppUserModelId)))
        {
            return liveSnapshot;
        }

        return TryCreateFallbackSnapshot(app);
    }

    public static CenterCardMediaSnapshot PreserveResolvedFields(
        CenterCardMediaSnapshot freshSnapshot,
        CenterCardMediaSnapshot? previousSnapshot)
    {
        if (!IsSameSong(freshSnapshot, previousSnapshot))
        {
            return freshSnapshot;
        }

        return freshSnapshot with
        {
            Lyric = string.IsNullOrWhiteSpace(freshSnapshot.Lyric)
                ? previousSnapshot!.Lyric
                : freshSnapshot.Lyric,
            Position = freshSnapshot.Position ?? previousSnapshot!.Position,
            Duration = freshSnapshot.Duration ?? previousSnapshot!.Duration
        };
    }

    public static bool SourceLooksLikeApp(RunningAppEntry app, string? sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
        {
            return false;
        }

        var source = NormalizeProbe(sourceAppUserModelId);
        return BuildAppProbeParts(app).Any(part => source.Contains(part) || part.Contains(source));
    }

    public static bool IsLikelyMusicApp(RunningAppEntry app)
    {
        var probe = NormalizeProbe($"{app.AppId} {app.DisplayName} {app.ExePath}");
        return MusicAppMarkers.Any(marker => probe.Contains(NormalizeProbe(marker)));
    }

    public static CenterCardMediaSnapshot? TryCreateFallbackSnapshot(RunningAppEntry app)
    {
        return null;
    }

    private static bool IsSameSong(CenterCardMediaSnapshot snapshot, CenterCardMediaSnapshot? previous)
    {
        return previous != null
            && string.Equals(snapshot.SourceAppUserModelId, previous.SourceAppUserModelId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(snapshot.Title, previous.Title, StringComparison.Ordinal)
            && string.Equals(snapshot.Artist, previous.Artist, StringComparison.Ordinal);
    }

    private static IEnumerable<string> BuildAppProbeParts(RunningAppEntry app)
    {
        foreach (var value in new[] { app.AppId, app.DisplayName, app.ExePath })
        {
            var normalized = NormalizeProbe(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }

            var fileName = NormalizeProbe(Path.GetFileNameWithoutExtension(value));
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                yield return fileName;
            }
        }
    }

    private static string NormalizeProbe(string? value)
    {
        return new string((value ?? string.Empty)
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }
}
