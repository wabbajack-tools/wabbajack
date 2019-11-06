using Wabbajack.Lib;

namespace Wabbajack
{
    public class InstallationConfigVM : ViewModel
    {
        private MainWindowVM _mainWindow;

        public InstallationConfigVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;
        }
    }
}
