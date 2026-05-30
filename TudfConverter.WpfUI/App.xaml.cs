using System;
using System.Threading.Tasks;
using System.Windows;

namespace TudfConverter.WpfUI
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ev) =>
            {
                MessageBox.Show($"A critical error occurred: {ev.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ev.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                ev.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex)
                {
                    // Log if needed
                }
            };

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}