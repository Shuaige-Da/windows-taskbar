using System.Windows;
using System.Windows.Threading;

namespace DynamicIslandBar
{
    public partial class App : System.Windows.Application
    {
        private SingleInstanceCoordinator? _singleInstance;
        private TrayIconService? _trayIcon;

        internal void AttachSingleInstance(SingleInstanceCoordinator singleInstance)
        {
            _singleInstance = singleInstance;
        }

        static App()
        {
            StartupEnvironment.EnsureWindowsFontEnvironment();
        }

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            StartupEnvironment.EnsureWindowsFontEnvironment();
            base.OnStartup(e);

            TaskbarRestoreWatchdog.StartForCurrentProcess();
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDiagnostics.Info("Lifecycle", "Application startup");

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            _trayIcon = new TrayIconService(
                Dispatcher,
                () => mainWindow.IsCapsuleVisible,
                mainWindow.OpenControlCenter,
                mainWindow.ToggleCapsuleVisibility,
                () =>
                {
                    TaskbarManager.Show();
                    Shutdown();
                });
            _singleInstance?.StartListening(() => Dispatcher.BeginInvoke(
                mainWindow.ShowCapsuleAndControlCenter));
            Dispatcher.BeginInvoke(
                () =>
                {
                    if (mainWindow.ShouldOpenControlCenterOnStartup)
                    {
                        mainWindow.OpenControlCenter();
                    }
                },
                DispatcherPriority.Loaded);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppDiagnostics.Info("Lifecycle", "Application exit");
            DispatcherUnhandledException -= App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            _trayIcon?.Dispose();
            _trayIcon = null;
            TaskbarManager.Show();
            base.OnExit(e);
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppDiagnostics.Error("Dispatcher", e.Exception);
            TaskbarManager.Show();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                AppDiagnostics.Error("AppDomain", exception);
            }
            else
            {
                AppDiagnostics.Warning("AppDomain", "Non-Exception unhandled failure");
            }
            TaskbarManager.Show();
        }

        private static void TaskScheduler_UnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs e)
        {
            AppDiagnostics.Error("TaskScheduler", e.Exception);
            e.SetObserved();
        }
    }
}
