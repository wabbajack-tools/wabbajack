using System;
using System.ComponentModel;
using System.Windows;
using MahApps.Metro.Controls;
using Wabbajack.Common;
using Application = System.Windows.Application;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private MainWindowVM _mwvm;
        private MainSettings _settings;

        public MainWindow()
        {
            _settings = MainSettings.LoadSettings();
            _mwvm = new MainWindowVM(this, _settings);
            DataContext = _mwvm;
            Wabbajack.Common.Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");
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
