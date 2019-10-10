using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wabbajack.Common;
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
            var bannerImage = UIUtils.BitmapImageFromResource("Wabbajack.banner_small.png");
            Banner.Source = bannerImage;
            var patreonIcon = UIUtils.BitmapImageFromResource("Wabbajack.Icons.patreon.png");
            Patreon.Source = patreonIcon;
            var githubIcon = UIUtils.BitmapImageFromResource("Wabbajack.Icons.github.png");
            GitHub.Source = githubIcon;
            var discordIcon = UIUtils.BitmapImageFromResource("Wabbajack.Icons.discord.png");
            Discord.Source = discordIcon;
        }

        private void CreateModlist_Click(object sender, RoutedEventArgs e)
        {
            var file = UIUtils.OpenFileDialog("MO2 Modlist(modlist.txt)|modlist.txt");
            if (file != null)
            {
                ShutdownOnClose = false;
                new MainWindow(RunMode.Compile, file).Start().Show();
                Close();
            }
        }

        private void InstallModlist_Click(object sender, RoutedEventArgs e)
        {
            var file = UIUtils.OpenFileDialog($"Wabbajack Modlist (*{Consts.ModlistExtension})|*{Consts.ModlistExtension}");
            if (file != null)
            {
                ShutdownOnClose = false;
                new MainWindow(RunMode.Install, file).Start().Show();
                Close();
            }
        }



        public void Close_Window(object sender, CancelEventArgs e)
        {
            if (ShutdownOnClose)
                Application.Current.Shutdown();
        }

        public bool ShutdownOnClose { get; set; } = true;

        private void GitHub_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/wabbajack-tools/wabbajack");
        }

        private void Patreon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://www.patreon.com/user?u=11907933");
        }

        private void Discord_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://discord.gg/zgbrkmA");
        }
    }
}
