using System.Windows;
using System.Windows.Threading;

namespace DynamicIslandBar
{
    public partial class App : System.Windows.Application
    {
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

            MainWindow = new MainWindow();
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TaskbarManager.Show();
            base.OnExit(e);
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            TaskbarManager.Show();
        }
    }
}
