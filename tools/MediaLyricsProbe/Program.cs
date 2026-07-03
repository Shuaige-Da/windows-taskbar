using DynamicIslandBar;

var title = args.ElementAtOrDefault(0) ?? "Run Tha Streetz";
var artist = args.ElementAtOrDefault(1) ?? "2Pac";
var positionSeconds = args.ElementAtOrDefault(2) is { } rawPosition && double.TryParse(rawPosition, out var parsedPosition)
    ? parsedPosition
    : 34;

string? netEaseLrc = null;
try
{
    netEaseLrc = await new NetEaseLyricsProvider().TryGetLrcAsync(title, artist, null);
    Console.WriteLine($"NetEase LRC length: {netEaseLrc?.Length ?? 0}");
}
catch (Exception ex)
{
    Console.WriteLine($"NetEase error: {ex}");
}

string? lrcLibLrc = null;
try
{
    lrcLibLrc = await new LrcLibLyricsProvider().TryGetLrcAsync(title, artist, null);
    Console.WriteLine($"LrcLib LRC length: {lrcLibLrc?.Length ?? 0}");
}
catch (Exception ex)
{
    Console.WriteLine($"LrcLib error: {ex}");
}

var provider = new CompositeLyricsProvider(
    new NetEaseLyricsProvider(),
    new LrcLibLyricsProvider());
var lrc = await provider.TryGetLrcAsync(title, artist, null);
Console.WriteLine($"LRC length: {lrc?.Length ?? 0}");
Console.WriteLine(lrc?.Split('\n').FirstOrDefault(line => line.Contains("All my homies", StringComparison.OrdinalIgnoreCase)) ?? "<no matching line>");
var lines = LrcParser.Parse(lrc);
Console.WriteLine($"Parsed lines: {lines.Count}");
Console.WriteLine($"At {positionSeconds:0.###}s: {LrcParser.ResolveCurrentLine(lines, TimeSpan.FromSeconds(positionSeconds))}");
foreach (var line in lines.Take(8))
{
    Console.WriteLine($"{line.Time}: {line.Text}");
}

var service = new LyricsService(new StaticLyricsProvider(lrc));
var snapshot = new CenterCardMediaSnapshot(
    IsMusicApp: true,
    IsPlaying: true,
    Title: title,
    Artist: artist,
    Lyric: string.Empty,
    SourceAppUserModelId: "cloudmusic.exe",
    Position: TimeSpan.FromSeconds(positionSeconds),
    Duration: TimeSpan.FromSeconds(316));
var resolved = await service.ResolveCurrentLyricAsync(snapshot);
Console.WriteLine($"Resolved lyric: {resolved.Lyric}");

internal sealed class StaticLyricsProvider(string? lrc) : ILyricsProvider
{
    public Task<string?> TryGetLrcAsync(
        string title,
        string artist,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(lrc);
    }
}
