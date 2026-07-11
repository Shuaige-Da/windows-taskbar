using DynamicIslandBar;
using System.Windows.Media;

namespace DynamicIslandBar.Tests;

public class CapsuleBackgroundImagePolicyTests
{
    [Theory]
    [InlineData(null, Stretch.UniformToFill)]
    [InlineData("Unknown", Stretch.UniformToFill)]
    [InlineData("UniformToFill", Stretch.UniformToFill)]
    [InlineData("Uniform", Stretch.Uniform)]
    [InlineData("Fill", Stretch.Fill)]
    public void MapStretch_NormalizesSupportedModes(string? configuredMode, Stretch expected)
    {
        Assert.Equal(expected, CapsuleBackgroundImagePolicy.MapStretch(configuredMode));
    }

    [Fact]
    public void IsSupportedImagePath_RequiresExistingSupportedFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"DynamicIslandBar-Image-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var imagePath = Path.Combine(directory, "background.png");
            var textPath = Path.Combine(directory, "background.txt");
            File.WriteAllBytes(imagePath, [1, 2, 3]);
            File.WriteAllText(textPath, "not image");

            Assert.True(CapsuleBackgroundImagePolicy.IsSupportedImagePath(imagePath));
            Assert.False(CapsuleBackgroundImagePolicy.IsSupportedImagePath(textPath));
            Assert.False(CapsuleBackgroundImagePolicy.IsSupportedImagePath(Path.Combine(directory, "missing.png")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BackgroundImageMutatorsClampOpacityAndNormalizeStretch()
    {
        var config = new CapsuleConfig();

        CapsuleConfigMutator.SetBackgroundImageOpacityPercent(config, 140);
        CapsuleConfigMutator.SetBackgroundImageStretchMode(config, "invalid");

        Assert.Equal(1, config.BackgroundImageOpacity);
        Assert.Equal("UniformToFill", config.BackgroundImageStretchMode);

        CapsuleConfigMutator.SetBackgroundImageOpacityPercent(config, -20);
        Assert.Equal(0, config.BackgroundImageOpacity);
    }

    [Fact]
    public void BackgroundImageSettings_RoundTripThroughConfiguration()
    {
        var config = new CapsuleConfig
        {
            BackgroundImagePath = @"C:\Images\capsule.png",
            BackgroundImageOpacity = 0.62,
            BackgroundImageStretchMode = "Uniform",
            ControlCenterBackgroundImagePath = @"C:\Images\home.png",
            ControlCenterBackgroundImageOpacity = 0.38,
            ControlCenterBackgroundImageStretchMode = "Fill"
        };

        var restored = CapsuleConfigSerializer.Deserialize(CapsuleConfigSerializer.Serialize(config));

        Assert.Equal(@"C:\Images\capsule.png", restored.BackgroundImagePath);
        Assert.Equal(0.62, restored.BackgroundImageOpacity, 2);
        Assert.Equal("Uniform", restored.BackgroundImageStretchMode);
        Assert.Equal(@"C:\Images\home.png", restored.ControlCenterBackgroundImagePath);
        Assert.Equal(0.38, restored.ControlCenterBackgroundImageOpacity, 2);
        Assert.Equal("Fill", restored.ControlCenterBackgroundImageStretchMode);
    }

    [Fact]
    public void LegacySharedBackgroundMigratesOnceButExplicitHomeRemovalIsPreserved()
    {
        var legacy = CapsuleConfigSerializer.Deserialize("""
            {
              "BackgroundImagePath": "C:\\Images\\legacy.png",
              "BackgroundImageOpacity": 0.55,
              "BackgroundImageStretchMode": "Uniform"
            }
            """);
        var explicitlyRemoved = CapsuleConfigSerializer.Deserialize("""
            {
              "BackgroundImagePath": "C:\\Images\\capsule.png",
              "ControlCenterBackgroundImagePath": null,
              "ControlCenterBackgroundImageOpacity": 0.4,
              "ControlCenterBackgroundImageStretchMode": "Fill"
            }
            """);

        Assert.Equal(@"C:\Images\legacy.png", legacy.ControlCenterBackgroundImagePath);
        Assert.Equal(0.55, legacy.ControlCenterBackgroundImageOpacity, 2);
        Assert.Equal("Uniform", legacy.ControlCenterBackgroundImageStretchMode);
        Assert.Null(explicitlyRemoved.ControlCenterBackgroundImagePath);
        Assert.Equal(0.4, explicitlyRemoved.ControlCenterBackgroundImageOpacity, 2);
        Assert.Equal("Fill", explicitlyRemoved.ControlCenterBackgroundImageStretchMode);
    }

    [Fact]
    public void ControlCenterBackgroundMutatorsAreIndependentFromCapsuleBackground()
    {
        var config = new CapsuleConfig { BackgroundImagePath = @"C:\Images\capsule.png" };

        CapsuleConfigMutator.SetControlCenterBackgroundImagePath(config, @"C:\Images\home.png");
        CapsuleConfigMutator.SetControlCenterBackgroundImageOpacityPercent(config, 140);
        CapsuleConfigMutator.SetControlCenterBackgroundImageStretchMode(config, "invalid");

        Assert.Equal(@"C:\Images\capsule.png", config.BackgroundImagePath);
        Assert.Equal(@"C:\Images\home.png", config.ControlCenterBackgroundImagePath);
        Assert.Equal(1, config.ControlCenterBackgroundImageOpacity);
        Assert.Equal("UniformToFill", config.ControlCenterBackgroundImageStretchMode);
    }

    [Fact]
    public void LoadFrozenImageSource_DecodesStreamWithoutUriCacheFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"DynamicIslandBar-Pixel-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(path, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

            var source = CapsuleBackgroundImagePolicy.LoadFrozenImageSource(path);

            Assert.True(source.IsFrozen);
            Assert.Equal(1, source.Width);
            Assert.Equal(1, source.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
