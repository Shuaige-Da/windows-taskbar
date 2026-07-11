using System.Text.RegularExpressions;

namespace DynamicIslandBar;

public readonly record struct LyricSearchQuery(string Title, string Artist);

public static class LyricSearchMetadataPolicy
{
    private static readonly string[] VersionSuffixPatterns =
    [
        @"\s*[（(].*?[)）]\s*$",
        @"\s*[\[【].*?[】\]]\s*$",
        @"\s+(?:feat\.?|ft\.?)\s+.+$",
        @"\s*[-–—|]\s*(?:official\s*)?(?:music\s*video|lyric\s*video|audio|mv)\s*$"
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

        var artistWasDerivedFromTitle = false;
        if (string.IsNullOrWhiteSpace(cleanArtist)
            && TrySplitTitleAndArtist(cleanTitle, out var splitTitle, out var splitArtist))
        {
            cleanTitle = splitTitle;
            cleanArtist = splitArtist;
            artistWasDerivedFromTitle = true;
        }

        return new LyricSearchIdentity(
            string.IsNullOrWhiteSpace(cleanTitle) ? title.Trim() : cleanTitle,
            cleanArtist,
            duration,
            artistWasDerivedFromTitle);
    }

    public static IReadOnlyList<LyricSearchQuery> BuildQueries(LyricSearchIdentity identity)
    {
        var queries = new List<LyricSearchQuery>();

        foreach (var artistVariant in BuildArtistVariants(identity.Artist))
        {
            AddQuery(queries, identity.Title, artistVariant);
        }
        AddQuery(queries, identity.Title, string.Empty);
        if (identity.ArtistWasDerivedFromTitle)
        {
            AddQuery(queries, identity.Artist, identity.Title);
            AddQuery(queries, identity.Artist, string.Empty);
        }

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

    private static IEnumerable<string> BuildArtistVariants(string artist)
    {
        var cleaned = CleanArtist(artist);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            yield break;
        }

        yield return cleaned;

        var primaryArtist = Regex.Split(
                cleaned,
                @"\s+(?:feat\.?|ft\.?)\s+|[,，/、;&＆]+",
                RegexOptions.IgnoreCase)
            .Select(part => part.Trim())
            .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part));
        if (!string.IsNullOrWhiteSpace(primaryArtist)
            && !string.Equals(primaryArtist, cleaned, StringComparison.OrdinalIgnoreCase))
        {
            yield return primaryArtist;
        }
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
            cleaned = Regex.Replace(cleaned, pattern, string.Empty, RegexOptions.IgnoreCase);
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
