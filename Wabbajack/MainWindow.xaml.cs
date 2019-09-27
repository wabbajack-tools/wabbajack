using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Alphaleonis.Win32.Filesystem;
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

        public enum RunMode
        {
            Compile,
            Install
        }

        public MainWindow(RunMode mode, string source)
        {
            var args = Environment.GetCommandLineArgs();
            var DebugMode = false;
            string MO2Folder = null, InstallFolder = null, MO2Profile = null;

            InitializeComponent();

            var context = new AppState(Dispatcher, "Building");
            context.LogMsg($"Wabbajack Build - {ThisAssembly.Git.Sha}");
            SetupHandlers(context);
            DataContext = context;
            WorkQueue.Init((id, msg, progress) => context.SetProgress(id, msg, progress),
                (max, current) => context.SetQueueSize(max, current));

            Utils.SetLoggerFn(s => context.LogMsg(s));
            Utils.SetStatusFn((msg, progress) => WorkQueue.Report(msg, progress));
            UIUtils.Dispatcher = Dispatcher;

            _state._nexusSiteURL = "https://github.com/halgari/wabbajack";

            new Thread(() =>
            {
                if (mode == RunMode.Compile)
                {
                    Utils.Log("Compiler ready to execute");
                    context.Location = Path.GetDirectoryName(source);
                }
                else if (mode == RunMode.Install)
                {
                    context.UIReady = false;
                    var modlist = Installer.LoadModlist(source);
                    if (modlist == null)
                    {
                        MessageBox.Show("Invalid Modlist, or file not found.", "Invalid Modlist", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Dispatcher.Invoke(() =>
                        {
                            context.Running = false;
                            ExitWhenClosing = false;
                            var window = new ModeSelectionWindow();
                            window.ShowActivated = true;
                            window.Show();
                            Close();
                        });
                    }
                    else
                    {
                        context.ConfigureForInstall(modlist);
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
            _state.LogMsg(((Exception) e.ExceptionObject).ExceptionToString());
        }


        internal bool ExitWhenClosing = true;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ExitWhenClosing)
                Application.Current.Shutdown();
        }
    }
}