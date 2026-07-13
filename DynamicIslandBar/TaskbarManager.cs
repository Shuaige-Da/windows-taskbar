using System.Runtime.InteropServices;

namespace DynamicIslandBar
{
    /// <summary>
    /// 控制 Windows 系统任务栏的显示和隐藏。
    /// </summary>
    public static class TaskbarManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter,
            string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string className, string windowTitle);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private static IntPtr GetTaskbarHandle()
        {
            // Windows 10 和 Windows 11 的主任务栏都使用此窗口类名。
            return FindWindow("Shell_TrayWnd", "");
        }

        private static IntPtr GetStartButtonHandle()
        {
            var handle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Button", "Start");
            if (handle == IntPtr.Zero)
            {
                // 兼容部分 Windows 11 版本的开始按钮窗口。
                handle = FindWindow("Start", "");
            }

            return handle;
        }

        /// <summary>
        /// 隐藏系统任务栏。
        /// </summary>
        public static void Hide()
        {
            var taskbar = GetTaskbarHandle();
            if (taskbar != IntPtr.Zero)
            {
                ShowWindow(taskbar, SW_HIDE);
            }

            var startBtn = GetStartButtonHandle();
            if (startBtn != IntPtr.Zero)
            {
                ShowWindow(startBtn, SW_HIDE);
            }
        }

        /// <summary>
        /// 显示系统任务栏。
        /// </summary>
        public static void Show()
        {
            var taskbar = GetTaskbarHandle();
            if (taskbar != IntPtr.Zero)
            {
                ShowWindow(taskbar, SW_SHOW);
            }

            var startBtn = GetStartButtonHandle();
            if (startBtn != IntPtr.Zero)
            {
                ShowWindow(startBtn, SW_SHOW);
            }
        }

        /// <summary>
        /// 根据指定状态显示或隐藏系统任务栏。
        /// </summary>
        public static void Toggle(bool show)
        {
            if (show)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }
    }
}
