using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIslandBar;

/// <summary>
/// Extracts dominant colors from album cover images for lyrics theming.
/// </summary>
public static class CoverColorExtractor
{
    public static List<Color> ExtractColors(BitmapSource bitmap, int count = 3)
    {
        var colors = new List<Color>();
        if (bitmap == null || bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
            return colors;

        try
        {
            var scaled = new TransformedBitmap(bitmap, new ScaleTransform(
                64.0 / bitmap.PixelWidth, 64.0 / bitmap.PixelHeight));

            var formatted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[formatted.PixelWidth * formatted.PixelHeight * 4];
            formatted.CopyPixels(pixels, formatted.PixelWidth * 4, 0);

            var buckets = new Dictionary<int, int>();
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];
                if (a < 128) continue;
                int lum = (r * 299 + g * 587 + b * 114) / 1000;
                if (lum < 30 || lum > 240) continue;

                int qr = r / 32, qg = g / 32, qb = b / 32;
                int key = (qr << 16) | (qg << 8) | qb;
                buckets.TryGetValue(key, out var cnt);
                buckets[key] = cnt + 1;
            }

            var sorted = buckets
                .OrderByDescending(kv => kv.Value)
                .Take(count * 3)
                .Select(kv =>
                {
                    int key = kv.Key;
                    byte r = (byte)(((key >> 16) & 0xFF) * 32 + 16);
                    byte g = (byte)(((key >> 8) & 0xFF) * 32 + 16);
                    byte b = (byte)((key & 0xFF) * 32 + 16);
                    return Color.FromRgb(r, g, b);
                })
                .ToList();

            foreach (var c in sorted)
            {
                if (colors.Count >= count) break;
                bool tooClose = colors.Any(existing =>
                    Math.Abs(existing.R - c.R) + Math.Abs(existing.G - c.G) + Math.Abs(existing.B - c.B) < 80);
                if (!tooClose)
                    colors.Add(c);
            }

            while (colors.Count < count)
                colors.Add(Colors.White);
        }
        catch
        {
            while (colors.Count < count)
                colors.Add(Colors.White);
        }

        return colors;
    }
}
