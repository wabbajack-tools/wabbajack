using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerConfigVM : ViewModel
    {
        private MainWindowVM _mainWindow;

        public IReactiveCommand BackCommand { get; }

        public CompilerConfigVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;

            BackCommand = ReactiveCommand.Create(() => { _mainWindow.CurrentPage = Page.StartUp; });
        }
    }
}
