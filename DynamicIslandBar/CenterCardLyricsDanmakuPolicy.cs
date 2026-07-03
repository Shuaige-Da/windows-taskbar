namespace DynamicIslandBar;

public readonly record struct LyricsDanmakuRegistration(
    bool ShouldEnqueue,
    int LaneIndex,
    int NextLaneIndex);

public static class CenterCardLyricsDanmakuPolicy
{
    public static LyricsDanmakuRegistration RegisterLyric(
        string? previousLyric,
        string? newLyric,
        int nextLaneIndex,
        int laneCount)
    {
        if (laneCount <= 0 || string.IsNullOrWhiteSpace(newLyric))
        {
            return new LyricsDanmakuRegistration(false, -1, Math.Max(0, nextLaneIndex));
        }

        if (string.Equals(previousLyric, newLyric, StringComparison.Ordinal))
        {
            return new LyricsDanmakuRegistration(false, -1, Math.Max(0, nextLaneIndex));
        }

        var laneIndex = Math.Clamp(nextLaneIndex, 0, laneCount - 1);
        return new LyricsDanmakuRegistration(
            true,
            laneIndex,
            (laneIndex + 1) % laneCount);
    }
}
