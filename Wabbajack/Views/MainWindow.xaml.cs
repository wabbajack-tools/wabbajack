using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Wabbajack.Common;
using Application = System.Windows.Application;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowVM _mainVM;
        private MainSettings _settings;

        public MainWindow()
        {
            _settings = MainSettings.LoadSettings();
            _mainVM = new MainWindowVM(this, _settings);
            DataContext = _mainVM;
            
            InitializeComponent();
        }

        internal bool ExitWhenClosing = true;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _mainVM.Dispose();
            MainSettings.SaveSettings(_settings);
            if (ExitWhenClosing)
            {
                Application.Current.Shutdown();
            }
        }
    }
}