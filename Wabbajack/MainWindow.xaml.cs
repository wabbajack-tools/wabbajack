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
            _mode = mode;
            _source = source;
            var args = Environment.GetCommandLineArgs();
            var DebugMode = false;
            string MO2Folder = null, InstallFolder = null, MO2Profile = null;

            InitializeComponent();
        }

        public MainWindow Start()
        {
            _context = new AppState(Dispatcher, "Building");
            DataContext = _context;
            WorkQueue.Init((id, msg, progress) => _context.SetProgress(id, msg, progress),
                (max, current) => _context.SetQueueSize(max, current));

            Utils.SetLoggerFn(s => _context.LogMsg(s));
            Utils.SetStatusFn((msg, progress) => WorkQueue.Report(msg, progress));
            UIUtils.Dispatcher = Dispatcher;

            _context._nexusSiteURL = "https://github.com/halgari/wabbajack";
            var thread = new Thread(() => { SetupWindow(_mode, _source, _context); });
            thread.Start();
            return this;
        }

        private void SetupWindow(RunMode mode, string source, AppState context)
        {
            if (mode == RunMode.Compile)
            {
                Utils.Log("Compiler ready to execute");
                context.Location = Path.GetDirectoryName(source);
            }
            else if (mode == RunMode.Install)
            {
                context.UIReady = false;
                var modlist = Installer.LoadFromFile(source);
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
                    context.ConfigureForInstall(source, modlist);
                }
            }

            context.UIReady = true;
        }


        internal bool ExitWhenClosing = true;
        private RunMode _mode;
        private string _source;
        private AppState _context;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ExitWhenClosing)
                Application.Current.Shutdown();
        }
    }
}