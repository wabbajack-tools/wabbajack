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
        private MainWindowVM _mwvm;

        public MainWindow()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length != 3) return;
            var modlistPath = args[2];
            Initialize(RunMode.Install, modlistPath);
        }

        public MainWindow(RunMode mode, string source)
        {
            Initialize(mode, source);
        }

        private void Initialize(RunMode mode, string source)
        {
            InitializeComponent();

            _mwvm = new MainWindowVM(mode, source, this);
            Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");
            this.DataContext = _mwvm;
        }

        internal bool ExitWhenClosing = true;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _mwvm.Dispose();
            if (ExitWhenClosing)
            {
                Application.Current.Shutdown();
            }
        }
    }
}