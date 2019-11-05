using System.ComponentModel;
using System.Windows;
using Application = System.Windows.Application;

namespace Wabbajack
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowVM _mainViewModel;
        private MainSettings _settings;

        public MainWindow()
        {
            _settings = MainSettings.LoadSettings();
            _mainViewModel = new MainWindowVM(this, _settings);
            DataContext = _mainViewModel;
            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InitializeComponent();
        }

        internal bool ExitWhenClosing = true;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _mainViewModel.Dispose();
            MainSettings.SaveSettings(_settings);
            if (ExitWhenClosing)
            {
                Application.Current.Shutdown();
            }
        }
    }
}