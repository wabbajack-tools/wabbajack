using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AppState _state;

        public MainWindow()
        {
            var args = Environment.GetCommandLineArgs();
            var DebugMode = false;
            string MO2Folder = null, InstallFolder = null, MO2Profile = null;

            if (args.Length > 1)
            {
                DebugMode = true;
                MO2Folder = args[1];
                MO2Profile = args[2];
                InstallFolder = args[3];
            }

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
                context.UIReady = false;
                var modlist = Installer.CheckForModList();
                if (modlist == null)
                    Utils.Log("No Modlist found, running in Compiler mode.");
                else
                    context.ConfigureForInstall(modlist);
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}