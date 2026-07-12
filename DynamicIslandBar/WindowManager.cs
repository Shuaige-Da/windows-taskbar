using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicIslandBar
{
    public static class WindowManager
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint GW_OWNER = 4;
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public sealed class WindowInfo
        {
            public IntPtr Handle { get; init; }
            public string Title { get; init; } = string.Empty;
            public int ProcessId { get; init; }
            public string ProcessName { get; init; } = string.Empty;
            public string? ExecutablePath { get; init; }
            public bool IsForeground { get; init; }
            public bool IsProcessMainWindow { get; init; }
            public bool IsToolWindow { get; init; }
            public bool IsNoActivateWindow { get; init; }
            public bool IsOwnedWindow { get; init; }
            public long WindowArea { get; init; }

            public void Activate()
            {
                ActivateWindow(Handle);
            }

            public void Minimize()
            {
                MinimizeWindow(Handle);
            }

            public void ToggleWindowState()
            {
                if (IsForeground && !IsIconic(Handle))
                {
                    Minimize();
                    return;
                }

                Activate();
            }
        }

        public static List<WindowInfo> GetVisibleWindows()
        {
            var windows = new List<WindowInfo>();
            var foregroundWindow = GetForegroundWindow();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                var style = GetWindowLong(hWnd, GWL_STYLE);
                if ((style & WS_VISIBLE) == 0)
                {
                    return true;
                }

                var length = GetWindowTextLength(hWnd);
                if (length == 0)
                {
                    return true;
                }

                var titleBuilder = new StringBuilder(length + 1);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                var title = titleBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    return true;
                }

                if (title is "Program Manager" or "Windows Input Experience")
                {
                    return true;
                }

                var processId = 0;
                var processName = string.Empty;
                string? executablePath = null;
                var isProcessMainWindow = false;
                try
                {
                    GetWindowThreadProcessId(hWnd, out var pid);
                    processId = (int)pid;
                    using var process = Process.GetProcessById(processId);
                    processName = process.ProcessName;
                    executablePath = TryGetProcessPath(process);
                    isProcessMainWindow = process.MainWindowHandle == hWnd;
                }
                catch
                {
                }

                var extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                var windowArea = GetWindowRect(hWnd, out var bounds)
                    ? Math.Max(0L, bounds.Right - bounds.Left) * Math.Max(0L, bounds.Bottom - bounds.Top)
                    : 0L;

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessId = processId,
                    ProcessName = processName,
                    ExecutablePath = executablePath,
                    IsForeground = hWnd == foregroundWindow,
                    IsProcessMainWindow = isProcessMainWindow,
                    IsToolWindow = (extendedStyle & WS_EX_TOOLWINDOW) != 0,
                    IsNoActivateWindow = (extendedStyle & WS_EX_NOACTIVATE) != 0,
                    IsOwnedWindow = GetWindow(hWnd, GW_OWNER) != IntPtr.Zero,
                    WindowArea = windowArea
                });

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public static bool ToggleWindowState(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            if (handle == GetForegroundWindow() && !IsIconic(handle))
            {
                return MinimizeWindow(handle);
            }

            return ActivateWindow(handle);
        }

        public static bool IsForegroundWindow(IntPtr handle)
        {
            return handle != IntPtr.Zero && handle == GetForegroundWindow();
        }

        public static bool ActivateWindow(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            ShowWindow(handle, SW_RESTORE);
            return SetForegroundWindow(handle);
        }

        public static bool MinimizeWindow(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            return ShowWindow(handle, SW_MINIMIZE);
        }

        public static bool CloseProcess(int processId)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return true;
                }

                if (process.CloseMainWindow())
                {
                    return true;
                }

                process.Kill(entireProcessTree: false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? TryGetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }
    }
}
