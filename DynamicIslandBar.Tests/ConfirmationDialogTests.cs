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
        return RepositoryFile.Read(parts);
    }
}
