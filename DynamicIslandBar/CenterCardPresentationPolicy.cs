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
    string? SourceAppUserModelId = null);

public sealed record CenterCardPresentation(
    CenterCardDisplayMode Mode,
    string PrimaryText,
    string SecondaryText,
    bool ShowLyricsMarquee,
    bool ShowTransportControls,
    bool ShowAppActions);

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
                ShowAppActions: false);
        }

        return new CenterCardPresentation(
            CenterCardDisplayMode.AppDetails,
            app.DisplayName,
            $"{status} · 单击激活 / 再次单击最小化",
            ShowLyricsMarquee: false,
            ShowTransportControls: false,
            ShowAppActions: true);
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
        if (!IsLikelyMusicApp(app))
        {
            return null;
        }

        return new CenterCardMediaSnapshot(
            IsMusicApp: true,
            IsPlaying: true,
            Title: "像鱼",
            Artist: "王贰浪",
            Lyric: "我在黄昏里等风经过");
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
