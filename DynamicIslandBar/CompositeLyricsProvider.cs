namespace DynamicIslandBar;

public sealed class CompositeLyricsProvider(params ILyricsProvider[] providers) : ILyricsProvider
{
    public async Task<string?> TryGetLrcAsync(
        string title,
        string artist,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? lrc;
            try
            {
                lrc = await provider.TryGetLrcAsync(title, artist, duration, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(lrc))
            {
                return lrc;
            }
        }

        return null;
    }
}
