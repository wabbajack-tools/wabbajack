using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class MainWindowVM : ViewModel
    {
        public MainWindow MainWindow { get; }

        public MainSettings Settings { get; }

        private readonly ObservableAsPropertyHelper<ViewModel> _ContentArea;
        public ViewModel ContentArea => _ContentArea.Value;

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            MainWindow = mainWindow;
            Settings = settings;
        }
    }
}
