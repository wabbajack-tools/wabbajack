using Wabbajack.Lib;

namespace Wabbajack
{
    public class StartupVM : ViewModel
    {
        public MainWindowVM MainWindow { get; }

        public StartupVM(MainWindowVM mainWindow)
        {
            MainWindow = mainWindow;
        }
    }
}
