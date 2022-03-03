using System.Windows;

namespace BfntConverterApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = new MainWindow(e.Args);
            mainWindow.Show();
        }
    }
}
