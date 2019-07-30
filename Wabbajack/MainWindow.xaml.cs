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
            InitializeComponent();

            var context = new AppState(Dispatcher, "Building");
            this.DataContext = context;

            WorkQueue.Init((id, msg, progress) => context.SetProgress(id, msg, progress));

            var compiler = new Compiler("c:\\Mod Organizer 2", msg => context.LogMsg(msg));

            compiler.MO2Profile = "DEV"; //"Basic Graphics and Fixes";
            context.ModListName = compiler.MO2Profile;
            context.Mode = "Building";


            new Thread(() =>
            {
                compiler.LoadArchives();
                compiler.Compile();



                compiler.ModList.ToJSON("C:\\tmp\\modpack.json");
                var modlist = compiler.ModList;
                var create = modlist.Directives.OfType<CreateBSA>().ToList();
                compiler = null;
                var installer = new Installer(modlist, "c:\\tmp\\install\\", msg => context.LogMsg(msg));
                installer.Install();

            }).Start();



        }
    }
}
