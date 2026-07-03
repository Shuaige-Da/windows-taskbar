using System.Text.RegularExpressions;

namespace DynamicIslandBar;

public sealed record LrcLine(TimeSpan Time, string Text);

public static partial class LrcParser
{
    public static IReadOnlyList<LrcLine> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return [];
        }

        var lines = new List<LrcLine>();
        foreach (var rawLine in lrc.Replace("\r\n", "\n").Split('\n'))
        {
            var matches = TimestampRegex().Matches(rawLine);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimestampRegex().Replace(rawLine, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in matches)
            {
                if (TryParseTimestamp(match, out var time))
                {
                    lines.Add(new LrcLine(time, text));
                }
            }
        }

        return lines
            .OrderBy(line => line.Time)
            .ToArray();
    }

    public static string ResolveCurrentLine(IReadOnlyList<LrcLine> lines, TimeSpan position)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var current = string.Empty;
        foreach (var line in lines)
        {
            if (line.Time > position)
            {
                break;
            }

            current = line.Text;
        }

        return current;
    }

    private static bool TryParseTimestamp(Match match, out TimeSpan time)
    {
        time = default;
        if (!int.TryParse(match.Groups["minutes"].Value, out var minutes)
            || !int.TryParse(match.Groups["seconds"].Value, out var seconds))
        {
            return false;
        }

        var fractionText = match.Groups["fraction"].Value;
        var milliseconds = fractionText.Length switch
        {
            0 => 0,
            1 => int.Parse(fractionText) * 100,
            2 => int.Parse(fractionText) * 10,
            _ => int.Parse(fractionText[..3])
        };
        time = TimeSpan.FromMinutes(minutes)
            + TimeSpan.FromSeconds(seconds)
            + TimeSpan.FromMilliseconds(milliseconds);
        return true;
    }

    [GeneratedRegex(@"\[(?<minutes>\d{1,3}):(?<seconds>\d{2})(?:[\.,](?<fraction>\d{1,3}))?\]")]
    private static partial Regex TimestampRegex();
}
