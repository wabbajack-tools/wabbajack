using Alphaleonis.Win32.Filesystem;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Lib;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowVM _mwvm;

        public MainWindow(RunMode mode, string source)
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