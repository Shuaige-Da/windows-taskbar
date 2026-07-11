using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIslandBar;

public static class CapsuleBackgroundImagePolicy
{
    public const string DefaultStretchMode = "UniformToFill";

    public static Stretch MapStretch(string? configuredMode)
    {
        return configuredMode?.Trim() switch
        {
            "Fill" => Stretch.Fill,
            "Uniform" => Stretch.Uniform,
            _ => Stretch.UniformToFill
        };
    }

    public static string NormalizeStretchMode(string? configuredMode)
    {
        return MapStretch(configuredMode) switch
        {
            Stretch.Fill => "Fill",
            Stretch.Uniform => "Uniform",
            _ => DefaultStretchMode
        };
    }

    public static bool IsSupportedImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp";
    }

    internal static ImageSource LoadFrozenImageSource(string path)
    {
        using var stream = File.OpenRead(path);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
