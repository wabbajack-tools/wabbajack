using System.Windows.Media.Imaging;
using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class CompilerConfigVM : ViewModel
    {
        private MainWindowVM _mainWindow;

        public BitmapImage MO2Image => UIUtils.BitmapImageFromResource("Wabbajack.Resources.MO2Button.png");
        public BitmapImage VortexImage => UIUtils.BitmapImageFromResource("Wabbajack.Resources.VortexButton.png");

        public IReactiveCommand BackCommand { get; }
        public IReactiveCommand CompileMO2 { get; }
        public IReactiveCommand CompileVortex { get; }

        public CompilerConfigVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;

            BackCommand = ReactiveCommand.Create(() => { _mainWindow.CurrentPage = Page.StartUp; });
        }
    }
}
