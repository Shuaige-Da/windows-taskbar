namespace DynamicIslandBar;

public readonly record struct LyricsFetchToken(string SongKey, long Generation);

public sealed class LyricsFetchCoordinator
{
    private long _currentGeneration;
    private string _currentSongKey = string.Empty;

    public LyricsFetchToken Begin(string songKey)
    {
        _currentSongKey = songKey;
        return new LyricsFetchToken(songKey, Interlocked.Increment(ref _currentGeneration));
    }

    public bool IsCurrent(LyricsFetchToken token)
    {
        return token.Generation == Volatile.Read(ref _currentGeneration)
            && string.Equals(token.SongKey, _currentSongKey, StringComparison.Ordinal);
    }

    public void Invalidate()
    {
        _currentSongKey = string.Empty;
        Interlocked.Increment(ref _currentGeneration);
    }
}
