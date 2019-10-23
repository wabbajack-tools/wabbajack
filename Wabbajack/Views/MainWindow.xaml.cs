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
            var args = Environment.GetCommandLineArgs();

            InitializeComponent();

            this._mwvm = new MainWindowVM(mode);
            var context = _mwvm.AppState;
            Utils.Log($"Wabbajack Build - {ThisAssembly.Git.Sha}");
            DataContext = _mwvm;

            new Thread(() =>
            {
                if (mode == RunMode.Compile)
                {
                    Utils.Log("Compiler ready to execute");
                    context.Location = Path.GetDirectoryName(source);
                    context.LocationLabel = "MO2 Profile:";
                }
                else if (mode == RunMode.Install)
                {
                    context.UIReady = false;
                    context.LocationLabel = "Installation Location:";
                    var modlist = Installer.LoadFromFile(source);
                    if (modlist == null)
                    {
                        MessageBox.Show("Invalid Modlist, or file not found.", "Invalid Modlist", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Dispatcher.Invoke(() =>
                        {
                            ExitWhenClosing = false;
                            var window = new ModeSelectionWindow
                            {
                                ShowActivated = true
                            };
                            window.Show();
                            Close();
                        });
                    }
                    else
                    {
                        context.ConfigureForInstall(source, modlist);
                    }

                }

                context.UIReady = true;
            }).Start();
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