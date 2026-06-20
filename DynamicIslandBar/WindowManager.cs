using System.Runtime.InteropServices;
using System.Text;

namespace DynamicIslandBar
{
    /// <summary>
    /// ö�ٲ������ǰ�򿪵Ĵ���
    /// </summary>
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

        private const int GWL_STYLE = -16;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_RESTORE = 9;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; } = "";

            public void Activate()
            {
                ShowWindow(Handle, SW_RESTORE);
                SetForegroundWindow(Handle);
            }
        }

        /// <summary>
        /// ��ȡ���пɼ����ڵı���;��
        /// </summary>
        public static List<WindowInfo> GetVisibleWindows()
        {
            var windows = new List<WindowInfo>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var style = GetWindowLong(hWnd, GWL_STYLE);
                if ((style & WS_VISIBLE) == 0) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                var sb = new StringBuilder(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);

                var title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title)) return true;

                // �ų�һЩϵͳ����
                if (title == "Program Manager" || title == "Windows Input Experience")
                    return true;

                windows.Add(new WindowInfo { Handle = hWnd, Title = title });
                return true;
            }, IntPtr.Zero);

            return windows;
        }
    }
}
