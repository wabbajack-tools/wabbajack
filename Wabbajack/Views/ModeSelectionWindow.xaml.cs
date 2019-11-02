using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.UI;
using static Wabbajack.MainWindow;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModeSelectionWindow.xaml
    /// </summary>
    public partial class ModeSelectionWindow : Window
    {
        private List<ModlistMetadata> _lists;

        public ModeSelectionWindow()
        {
            InitializeComponent();
            var bannerImage = UIUtils.BitmapImageFromResource("Wabbajack.Resources.banner_small_dark.png");
            Banner.Source = bannerImage;
            var patreonIcon = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Icons.patreon_light.png");
            Patreon.Source = patreonIcon;
            var githubIcon = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Icons.github_light.png");
            GitHub.Source = githubIcon;
            var discordIcon = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Icons.discord.png");
            Discord.Source = discordIcon;

            DataContext = new ModeSelectionWindowVM();
        }

        private void CreateModlist_Click(object sender, RoutedEventArgs e)
        {
            OpenMainWindow(
                RunMode.Compile,
                UIUtils.OpenFileDialog("MO2 Modlist(modlist.txt)|modlist.txt"));
        }

        private void InstallModlist_Click(object sender, RoutedEventArgs e)
        {
            //OpenMainWindow(
            //    RunMode.Install,
            //    UIUtils.OpenFileDialog($"Wabbajack Modlist (*{Consts.ModlistExtension})|*{Consts.ModlistExtension}"));

            var result = ((ModeSelectionWindowVM)DataContext).Download();
            if (result != null)
            {
                OpenMainWindow(RunMode.Install, result);
            }
        }

        private void OpenMainWindow(RunMode mode, string file)
        {
            if (file == null) return;
            ShutdownOnClose = false;
            var window = new MainWindow(mode, file);
            window.Left = this.Left;
            window.Top = this.Top;
            window.Show();
            Close();
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

        private void InstallFromList_Click(object sender, RoutedEventArgs e)
        {
            OpenMainWindow(RunMode.Install, 
                UIUtils.OpenFileDialog($"*{ExtensionManager.Extension}|*{ExtensionManager.Extension}"));
        }
    }
}
