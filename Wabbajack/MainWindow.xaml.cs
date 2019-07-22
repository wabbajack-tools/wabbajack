using System;
using System.Collections.Generic;
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
            WorkQueue.Init((id, msg, progress) => context.SetProgress(id, msg, progress));

            var compiler = new Compiler("c:\\Mod Organizer 2", msg => context.LogMsg(msg));
            new Thread(() =>
            {
                compiler.LoadArchives();
                compiler.MO2Profile = "Lexy's Legacy of The Dragonborn Special Edition";
                compiler.Compile();
            }).Start();


            this.DataContext = context;

        }
    }
}
