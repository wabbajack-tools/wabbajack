using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class StartupVM : ViewModel
    {
        private MainWindowVM _mainWindow;
        public IReactiveCommand OpenModListGallery { get; }
        public IReactiveCommand InstallFromFile { get; }
        public IReactiveCommand CompileModList { get; }

        public StartupVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;
            OpenModListGallery = ReactiveCommand.Create(() => { _mainWindow.Page = 1; });
            InstallFromFile = ReactiveCommand.Create(() => { _mainWindow.Page = 2; });
            CompileModList = ReactiveCommand.Create(() => { _mainWindow.Page = 3; });
        }
    }
}
