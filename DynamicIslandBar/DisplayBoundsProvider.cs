using System.Runtime.InteropServices;
using System.Windows;

namespace DynamicIslandBar;

public static class DisplayBoundsProvider
{
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static (double Width, double Height) GetPrimaryScreenSize()
    {
        if (SystemParameters.PrimaryScreenWidth > 0 && SystemParameters.PrimaryScreenHeight > 0)
        {
            return (SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        }

        var width = GetSystemMetrics(SmCxScreen);
        var height = GetSystemMetrics(SmCyScreen);

        if (width > 0 && height > 0)
        {
            return (width, height);
        }

        return (1920, 1080);
    }
}
