using System.Reflection;

namespace DynamicIslandBar.Tests;

public class LrcParserTests
{
    [Fact]
    public void ResolveCurrentLine_ReturnsLatestLyricAtPlaybackPosition()
    {
        var entries = Parse(
            """
            [ar:王贰浪]
            [00:10.00]第一句歌词
            [00:12.30]我在黄昏里等风经过
            [00:18.00]下一句歌词
            """);

        Assert.Equal("第一句歌词", ResolveCurrentLine(entries, TimeSpan.FromSeconds(10)));
        Assert.Equal("我在黄昏里等风经过", ResolveCurrentLine(entries, TimeSpan.FromSeconds(12.5)));
        Assert.Equal("下一句歌词", ResolveCurrentLine(entries, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void ResolveCurrentLine_ReturnsEmptyBeforeFirstLyric()
    {
        var entries = Parse("[00:12.30]我在黄昏里等风经过");

        Assert.Equal(string.Empty, ResolveCurrentLine(entries, TimeSpan.FromSeconds(5)));
    }

    private static object Parse(string lrc)
    {
        var parserType = GetParserType();
        var parse = parserType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(parse);
        return parse!.Invoke(null, [lrc])!;
    }

    private static string ResolveCurrentLine(object entries, TimeSpan position)
    {
        var parserType = GetParserType();
        var resolve = parserType.GetMethod("ResolveCurrentLine", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(resolve);
        return Assert.IsType<string>(resolve!.Invoke(null, [entries, position]));
    }

    private static Type GetParserType()
    {
        var type = typeof(CenterCardPresentationPolicy).Assembly.GetType("DynamicIslandBar.LrcParser");
        Assert.NotNull(type);
        return type!;
    }
}
