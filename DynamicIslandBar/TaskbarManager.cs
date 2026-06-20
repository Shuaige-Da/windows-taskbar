using System.Runtime.InteropServices;

namespace DynamicIslandBar
{
    /// <summary>
    /// ���� Windows ϵͳ����������ʾ/����
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
            // Windows 11 �������������� "Shell_TrayWnd"
            // Windows 10 Ҳ�� "Shell_TrayWnd"
            return FindWindow("Shell_TrayWnd", "");
        }

        private static IntPtr GetStartButtonHandle()
        {
            // Windows 11 ��ʼ��ť
            var handle = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Button", "Start");
            if (handle == IntPtr.Zero)
            {
                // ���� Windows 11 ���¿�ʼ��ť����
                handle = FindWindow("Start", "");
            }
            return handle;
        }

        /// <summary>
        /// ����ϵͳ������
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
        /// ��ʾϵͳ������
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
        /// �л���������ʾ״̬
        /// </summary>
        public static void Toggle(bool show)
        {
            if (show)
                Show();
            else
                Hide();
        }
    }
}
