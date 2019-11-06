using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class StartupVM : ViewModel
    {
        private MainWindowVM _mainWindow;
        public IReactiveCommand InstallFile { get; }

        public StartupVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;
            InstallFile = ReactiveCommand.Create(() => {Utils.LogToFile("hi"); });
        }

        
    }
}
