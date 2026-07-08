using System.Runtime.InteropServices;

namespace DynamicIslandBar;

/// <summary>
/// Converts between Traditional and Simplified Chinese using Win32 LCMapString API.
/// </summary>
public static class ChineseConverter
{
    private const int LCMAP_SIMPLIFIED_CHINESE = 0x02000000;
    private const int LCMAP_TRADITIONAL_CHINESE = 0x04000000;
    private const int LCMAP_LCID = 0x00020000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int LCMapString(
        int locale,
        int flags,
        string src,
        int srcLen,
        [Out] char[]? dst,
        int dstLen);

    /// <summary>
    /// Convert Traditional Chinese text to Simplified Chinese.
    /// </summary>
    public static string ToSimplified(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            // LCID 0x0804 = Chinese (Simplified, China)
            int len = LCMapString(0x0804, LCMAP_SIMPLIFIED_CHINESE, text, text.Length, null, 0);
            if (len <= 0)
                return text;

            var buffer = new char[len];
            LCMapString(0x0804, LCMAP_SIMPLIFIED_CHINESE, text, text.Length, buffer, len);
            return new string(buffer);
        }
        catch
        {
            return text;
        }
    }

    /// <summary>
    /// Convert Simplified Chinese text to Traditional Chinese.
    /// </summary>
    public static string ToTraditional(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            // LCID 0x0404 = Chinese (Traditional, Taiwan)
            int len = LCMapString(0x0404, LCMAP_TRADITIONAL_CHINESE, text, text.Length, null, 0);
            if (len <= 0)
                return text;

            var buffer = new char[len];
            LCMapString(0x0404, LCMAP_TRADITIONAL_CHINESE, text, text.Length, buffer, len);
            return new string(buffer);
        }
        catch
        {
            return text;
        }
    }
}

public enum LyricLanguage
{
    Simplified,
    Traditional
}
