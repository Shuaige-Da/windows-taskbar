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
        // Core music keywords
        "music",
        // Chinese music apps
        "cloudmusic",      // NetEase Cloud Music
        "qqmusic",         // QQ Music
        "kugou",           // Kugou
        "kuwo",            // Kuwo
        "网易云",           // NetEase Cloud Music (Chinese)
        "qq音乐",          // QQ Music (Chinese)
        "酷狗",            // Kugou (Chinese)
        "酷我",            // Kuwo (Chinese)
        // International music apps
        "spotify",
        "applemusic",      // Apple Music
        "applemusicwin",   // Apple Music for Windows
        "youtubemusic",    // YouTube Music
        "amazonmusic",     // Amazon Music
        "tidal",
        "deezer",
        "pandora",
        "soundcloud",
        "deezer",          // Deezer
        "iheartradio",
        "napster",
        // Audio players
        "audacity",
        "aimp",
        "foobar2000",
        "musicbee",
        "winamp",
        "vlc",             // VLC (often used for music)
        "potplayer",       // PotPlayer (media player)
        "kmplayer",
        "gomplayer",
        "mediaplayer",
        "audioplayer",
        // Chinese names
        "apple音乐",       // Apple Music (Chinese)
        "youtube音乐",     // YouTube Music (Chinese)
        "亚马逊音乐",      // Amazon Music (Chinese)
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
        if (MusicAppMarkers.Any(marker => probe.Contains(NormalizeProbe(marker))))
            return true;

        // Additional heuristic: check exe filename against known music player patterns
        if (!string.IsNullOrWhiteSpace(app.ExePath))
        {
            var exeName = NormalizeProbe(Path.GetFileNameWithoutExtension(app.ExePath));
            if (!string.IsNullOrWhiteSpace(exeName) && exeName.Length > 2)
            {
                // Check if the exe name contains any marker substring
                if (MusicAppMarkers.Any(marker => exeName.Contains(NormalizeProbe(marker))))
                    return true;
            }
        }

        return false;
    }

    public static CenterCardMediaSnapshot? TryCreateFallbackSnapshot(RunningAppEntry app)
    {
        if (!IsLikelyMusicApp(app))
        {
            return null;
        }

        return null;
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
