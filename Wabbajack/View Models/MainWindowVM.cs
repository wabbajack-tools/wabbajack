using ReactiveUI;
using Wabbajack.Lib;

namespace Wabbajack
{
    public class MainWindowVM : ViewModel
    {
        public MainWindow MainWindow { get; }

        public MainSettings Settings { get; }

        private readonly ObservableAsPropertyHelper<ViewModel> _ActivePane;
        public ViewModel ActivePane => _ActivePane.Value;

        public MainWindowVM(MainWindow mainWindow, MainSettings settings)
        {
            MainWindow = mainWindow;
            Settings = settings;
        }
    }
}
