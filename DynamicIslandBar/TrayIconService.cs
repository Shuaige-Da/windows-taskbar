using System.Drawing;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DynamicIslandBar;

internal sealed class TrayIconService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<bool> _isCapsuleVisible;
    private readonly Action _openControlCenter;
    private readonly Action _toggleCapsuleVisibility;
    private readonly Action _exitApplication;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _capsuleVisibilityItem;
    private readonly Forms.ToolStripMenuItem _startupItem;
    private readonly Icon _icon;
    private bool _disposed;

    public TrayIconService(
        Dispatcher dispatcher,
        Func<bool> isCapsuleVisible,
        Action openControlCenter,
        Action toggleCapsuleVisibility,
        Action exitApplication)
    {
        _dispatcher = dispatcher;
        _isCapsuleVisible = isCapsuleVisible;
        _openControlCenter = openControlCenter;
        _toggleCapsuleVisibility = toggleCapsuleVisibility;
        _exitApplication = exitApplication;
        _icon = LoadApplicationIcon();

        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("打开控制中心");
        openItem.Click += (_, _) => Dispatch(_openControlCenter);
        _capsuleVisibilityItem = new Forms.ToolStripMenuItem("隐藏胶囊");
        _capsuleVisibilityItem.Click += (_, _) => Dispatch(_toggleCapsuleVisibility);
        _startupItem = new Forms.ToolStripMenuItem("开机自启");
        _startupItem.Click += (_, _) => Dispatch(ToggleStartupRegistration);
        var exitItem = new Forms.ToolStripMenuItem("退出程序");
        exitItem.Click += (_, _) => Dispatch(_exitApplication);
        menu.Items.Add(openItem);
        menu.Items.Add(_capsuleVisibilityItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => RefreshMenuState();

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "胶囊控制中心",
            Icon = _icon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatch(_openControlCenter);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    private void RefreshMenuState()
    {
        _capsuleVisibilityItem.Text = _isCapsuleVisible() ? "隐藏胶囊" : "显示胶囊";
        _startupItem.Checked = StartupRegistrationService.IsEnabled();
    }

    private void ToggleStartupRegistration()
    {
        StartupRegistrationService.SetEnabled(!StartupRegistrationService.IsEnabled());
    }

    private void Dispatch(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.BeginInvoke(action);
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                using var icon = Icon.ExtractAssociatedIcon(processPath);
                if (icon != null)
                {
                    return (Icon)icon.Clone();
                }
            }
        }
        catch
        {
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
