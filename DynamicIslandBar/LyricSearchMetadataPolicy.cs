using System.Text.RegularExpressions;

namespace DynamicIslandBar;

public readonly record struct LyricSearchQuery(string Title, string Artist);

public static class LyricSearchMetadataPolicy
{
    private static readonly string[] VersionSuffixPatterns =
    [
        @"\s*[（(].*?[)）]\s*$",
        @"\s*[\[【].*?[】\]]\s*$"
    ];

    private static readonly string[] TitleArtistSeparators =
    [
        " - ",
        " – ",
        " — ",
        " | ",
        " / "
    ];

    public static LyricSearchIdentity BuildIdentity(string title, string artist, TimeSpan duration)
    {
        var cleanTitle = CleanTitle(title);
        var cleanArtist = CleanArtist(artist);

        if (string.IsNullOrWhiteSpace(cleanArtist)
            && TrySplitTitleAndArtist(cleanTitle, out var splitTitle, out var splitArtist))
        {
            cleanTitle = splitTitle;
            cleanArtist = splitArtist;
        }

        return new LyricSearchIdentity(
            string.IsNullOrWhiteSpace(cleanTitle) ? title.Trim() : cleanTitle,
            cleanArtist,
            duration);
    }

    public static IReadOnlyList<LyricSearchQuery> BuildQueries(LyricSearchIdentity identity)
    {
        var queries = new List<LyricSearchQuery>();

        AddQuery(queries, identity.Title, identity.Artist);
        AddQuery(queries, identity.Title, string.Empty);

        var cleanedTitle = CleanTitle(identity.Title);
        if (!string.Equals(cleanedTitle, identity.Title, StringComparison.OrdinalIgnoreCase))
        {
            AddQuery(queries, cleanedTitle, identity.Artist);
            AddQuery(queries, cleanedTitle, string.Empty);
        }

        if (TrySplitTitleAndArtist(identity.Title, out var splitTitle, out var splitArtist))
        {
            AddQuery(queries, splitTitle, string.IsNullOrWhiteSpace(identity.Artist) ? splitArtist : identity.Artist);
            AddQuery(queries, splitTitle, string.Empty);
        }

        return queries;
    }

    public static string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var cleaned = title.Trim();
        foreach (var pattern in VersionSuffixPatterns)
        {
            cleaned = Regex.Replace(cleaned, pattern, string.Empty);
        }

        cleaned = Regex.Replace(cleaned, @"[★☆♪♫♬♩✦✧]", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^[\.\-\s]+|[\.\-\s]+$", string.Empty);
        return cleaned.Trim();
    }

    private static string CleanArtist(string artist)
    {
        return string.IsNullOrWhiteSpace(artist)
            ? string.Empty
            : artist.Trim();
    }

    private static bool TrySplitTitleAndArtist(string title, out string titlePart, out string artistPart)
    {
        titlePart = string.Empty;
        artistPart = string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        foreach (var separator in TitleArtistSeparators)
        {
            var parts = title.Split(separator, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var left = CleanTitle(parts[0]);
            var right = CleanArtist(parts[1]);
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                continue;
            }

            titlePart = left;
            artistPart = right;
            return true;
        }

        return false;
    }

    private static void AddQuery(List<LyricSearchQuery> queries, string title, string artist)
    {
        var cleanTitle = CleanTitle(title);
        var cleanArtist = CleanArtist(artist);
        if (string.IsNullOrWhiteSpace(cleanTitle))
        {
            return;
        }

        var query = new LyricSearchQuery(cleanTitle, cleanArtist);
        if (!queries.Contains(query))
        {
            queries.Add(query);
        }
    }
}
