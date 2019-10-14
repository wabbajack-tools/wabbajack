using Alphaleonis.Win32.Filesystem;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Wabbajack.Common;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppState _state;

        public MainWindow(RunMode mode, string source)
        {
            var args = Environment.GetCommandLineArgs();

            InitializeComponent();

            var context = new AppState(RunMode.Install);
            context.LogMsg($"Wabbajack Build - {ThisAssembly.Git.Sha}");
            SetupHandlers(context);
            DataContext = context;

            Utils.SetLoggerFn(s => context.LogMsg(s));
            Utils.SetStatusFn((msg, progress) => WorkQueue.Report(msg, progress));

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

        private void SetupHandlers(AppState state)
        {
            _state = state;
            AppDomain.CurrentDomain.UnhandledException += AppHandler;
        }

        private void AppHandler(object sender, UnhandledExceptionEventArgs e)
        {
            _state.LogMsg("Uncaught error:");
            _state.LogMsg(((Exception)e.ExceptionObject).ExceptionToString());
        }


        internal bool ExitWhenClosing = true;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ExitWhenClosing)
                Application.Current.Shutdown();
        }
    }
}