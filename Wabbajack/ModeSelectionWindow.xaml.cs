using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Wabbajack.MainWindow;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModeSelectionWindow.xaml
    /// </summary>
    public partial class ModeSelectionWindow : Window
    {
        public ModeSelectionWindow()
        {
            InitializeComponent();
            var img = UIUtils.BitmapImageFromResource("Wabbajack.banner_small.png");
            Banner.Source = img;
        }

        private void CreateModlist_Click(object sender, RoutedEventArgs e)
        {
            var file = UIUtils.OpenFileDialog("MO2 Modlist(modlist.txt)|modlist.txt");
            if (file != null)
            {
                ShutdownOnClose = false;
                new MainWindow(RunMode.Compile, file).Show();
                Close();
            }
        }

        private void InstallModlist_Click(object sender, RoutedEventArgs e)
        {
            var file = UIUtils.OpenFileDialog("Wabbajack Modlist (*.modlist)|*.modlist");
            if (file != null)
            {
                ShutdownOnClose = false;
                new MainWindow(RunMode.Install, file).Show();
                Close();
            }
        }



        public void Close_Window(object sender, CancelEventArgs e)
        {
            if (ShutdownOnClose)
                Application.Current.Shutdown();
        }

        public bool ShutdownOnClose { get; set; } = true;
    }
}
