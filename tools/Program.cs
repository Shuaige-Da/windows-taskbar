using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

class Program
{
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    const int SRCCOPY = 0x00CC0020;

    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "screenshot.png";
        string processName = args.Length > 1 ? args[1] : "DynamicIslandBar";

        var procs = Process.GetProcessesByName(processName);
        if (procs.Length == 0)
        {
            Console.WriteLine($"Process '{processName}' not found.");
            return;
        }

        int targetPid = procs[0].Id;
        IntPtr foundHwnd = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out int pid);
            if (pid == targetPid && IsWindowVisible(hWnd))
            {
                GetWindowRect(hWnd, out RECT rect);
                int w = rect.Right - rect.Left;
                int h = rect.Bottom - rect.Top;
                if (w > 0 && h > 0)
                {
                    foundHwnd = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);

        if (foundHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"No visible window found for process '{processName}'.");
            return;
        }

        GetWindowRect(foundHwnd, out RECT winRect);
        int width = winRect.Right - winRect.Left;
        int height = winRect.Bottom - winRect.Top;
        Console.WriteLine($"Found window: {width}x{height} at ({winRect.Left},{winRect.Top})");

        IntPtr hdcScreen = GetDC(IntPtr.Zero);
        IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, width, height);
        IntPtr hOld = SelectObject(hdcMem, hBitmap);

        // PW_RENDERFULLCONTENT = 0x00000002
        bool ok = PrintWindow(foundHwnd, hdcMem, 0x00000002);
        if (!ok)
        {
            // Fallback: try BitBlt from screen DC
            BitBlt(hdcMem, 0, 0, width, height, hdcScreen, winRect.Left, winRect.Top, SRCCOPY);
        }

        SelectObject(hdcMem, hOld);

        using Bitmap bmp = Image.FromHbitmap(hBitmap);
        bmp.Save(path, ImageFormat.Png);

        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(IntPtr.Zero, hdcScreen);

        Console.WriteLine($"Screenshot saved: {path} ({width}x{height})");
    }
}
