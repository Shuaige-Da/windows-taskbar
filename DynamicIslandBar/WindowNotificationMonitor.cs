using System.Runtime.InteropServices;

namespace DynamicIslandBar;

internal sealed class WindowNotificationMonitor : IDisposable
{
    private const uint EventSystemAlert = 0x0002;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WineventSkipOwnProcess = 0x0002;

    private readonly WinEventDelegate _callback;
    private IntPtr _hook;

    public WindowNotificationMonitor()
    {
        _callback = HandleWinEvent;
        _hook = SetWinEventHook(
            EventSystemAlert,
            EventSystemAlert,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WineventOutOfContext | WineventSkipOwnProcess);
    }

    public event Action<int>? ProcessAlerted;

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    private void HandleWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId != 0)
        {
            ProcessAlerted?.Invoke((int)processId);
        }
    }

    private delegate void WinEventDelegate(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr eventHookAssembly,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}

public static class AppNotificationAnimationPolicy
{
    public static bool ShouldAnimate(
        DateTime? previousAnimationUtc,
        DateTime currentUtc,
        TimeSpan minimumInterval)
    {
        return previousAnimationUtc is null
            || currentUtc - previousAnimationUtc.Value >= minimumInterval;
    }
}
