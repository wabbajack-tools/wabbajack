using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.UI;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModeSelectionWindow.xaml
    /// </summary>
    public partial class ModeSelectionWindow : Window
    {
        MainSettings settings;

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

            settings = MainSettings.LoadSettings();
            DataContext = new ModeSelectionWindowVM();
        }

        private void CreateModlist_Click(object sender, RoutedEventArgs e)
        {
            ShutdownOnClose = false;
            var window = new MainWindow(RunMode.Compile, null, settings);
            window.Left = Left;
            window.Top = Top;
            window.Show();
            Close();
        }

        private void InstallModlist_Click(object sender, RoutedEventArgs e)
        {
            var result = ((ModeSelectionWindowVM)DataContext).Download();
            if (result != null)
            {
                OpenMainWindowInstall(result);
            }
        }

        private void InstallFromList_Click(object sender, RoutedEventArgs e)
        {
            OpenMainWindowInstall(
                UIUtils.OpenFileDialog(
                    $"*{ExtensionManager.Extension}|*{ExtensionManager.Extension}",
                    initialDirectory: settings.Installer.LastInstalledListLocation));
        }

        private void OpenMainWindowInstall(string file)
        {
            if (file == null) return;
            ShutdownOnClose = false;
            settings.Installer.LastInstalledListLocation = Path.GetDirectoryName(file);
            var window = new MainWindow(RunMode.Install, file, settings);
            window.Left = Left;
            window.Top = Top;
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
    }
}
