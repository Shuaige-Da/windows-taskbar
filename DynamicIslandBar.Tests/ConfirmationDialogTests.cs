namespace DynamicIslandBar.Tests;

public class ConfirmationDialogTests
{
    [Fact]
    public void ConfirmationDialog_UsesUnfilledGlassSurfaceAndThemeAccent()
    {
        var code = ReadProjectFile("DynamicIslandBar", "CapsuleConfirmationDialog.xaml.cs");

        Assert.Contains("DialogSurface.Background = Brushes.Transparent;", code);
        Assert.Contains("DialogIconSurface.Background = Brushes.Transparent;", code);
        Assert.Contains("isWhite ? \"#FFFFFFFF\" : \"#FF46E0FF\"", code);
        Assert.DoesNotContain("new LinearGradientBrush(", code);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var path = Path.Combine([FindRepositoryRoot(), .. parts]);
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "DynamicIslandBar")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
