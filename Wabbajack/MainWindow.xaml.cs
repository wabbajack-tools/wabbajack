using System;
using System.Collections.Generic;
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
            this.DataContext = context;
            WorkQueue.Init((id, msg, progress) => context.SetProgress(id, msg, progress),
                           (max, current) => context.SetQueueSize(max, current));


            if (DebugMode)
            {
                new Thread(() =>
                {
                    var compiler = new Compiler(MO2Folder, msg => context.LogMsg(msg));

                    compiler.MO2Profile = MO2Profile;
                    context.ModListName = compiler.MO2Profile;

                    context.Mode = "Building";
                    compiler.LoadArchives();
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
                    }
                    else
                    {
                        context.ConfigureForInstall(modlist);
                    }
                }).Start();

            }
        }
    }
}
