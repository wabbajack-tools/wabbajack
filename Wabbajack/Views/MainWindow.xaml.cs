using System;
using System.ComponentModel;
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
        private MainWindowVM _mwvm;
        private MainSettings _settings;

        public MainWindow()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length != 3) return;
            var modlistPath = args[2];
            _settings = MainSettings.LoadSettings();
            Initialize(RunMode.Install, modlistPath, _settings);
        }

        public MainWindow(RunMode mode, string source, MainSettings settings)
        {
            Initialize(mode, source, settings);
        }

        private void Initialize(RunMode mode, string source, MainSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _mwvm = new MainWindowVM(mode, source, this, settings);
            Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");
            DataContext = _mwvm;
        }

        internal bool ExitWhenClosing = true;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _mwvm.Dispose();
            MainSettings.SaveSettings(_settings);
            if (ExitWhenClosing)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
