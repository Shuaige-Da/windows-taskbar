using System.Windows;

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

            MainWindow = new MainWindow();
            MainWindow.Show();
        }
    }
}
