using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {

            var args = Environment.GetCommandLineArgs();
            bool DebugMode = false;
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
            this.DataContext = context;
            WorkQueue.Init((id, msg, progress) => context.SetProgress(id, msg, progress),
                           (max, current) => context.SetQueueSize(max, current));

            Utils.SetLoggerFn(s => context.LogMsg(s));
            Utils.SetStatusFn((msg, progress) => WorkQueue.Report(msg, progress));



            if (DebugMode)
            {
                new Thread(() =>
                {
                    var compiler = new Compiler(MO2Folder, msg => context.LogMsg(msg));

                    compiler.MO2Profile = MO2Profile;
                    context.ModListName = compiler.MO2Profile;

                    context.Mode = "Building";
                    compiler.Compile();

                    var modlist = compiler.ModList.ToJSON();
                    compiler = null;

                    context.ConfigureForInstall(modlist);

                }).Start();
            }
            else
            {
                new Thread(() =>
                {
                    var modlist = Installer.CheckForModPack();
                    if (modlist == null)
                    {
                        Utils.Log("No Modlist found, running in Compiler mode.");
                    }
                    else
                    {
                        context.ConfigureForInstall(modlist);

                    }
                }).Start();

            }
        }

        private AppState _state;
        private void SetupHandlers(AppState state)
        {
            _state = state;
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AppHandler);
        }

        private void AppHandler(object sender, UnhandledExceptionEventArgs e)
        {
            _state.LogMsg("Uncaught error:");
            _state.LogMsg(Utils.ExceptionToString((Exception)e.ExceptionObject));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
