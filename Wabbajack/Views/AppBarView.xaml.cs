using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using Wabbajack.Lib;

namespace Wabbajack.Views
{
    public partial class AppBarView : UserControl
    {
        public AppBarView()
        {
            InitializeComponent();
            Github.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Icons.github_light.png");
            Patreon.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Icons.patreon_light.png");
            Discord.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Icons.discord.png");
        }

        private void Github_OnClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/wabbajack-tools/wabbajack");
        }

        private void Patreon_OnClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://www.patreon.com/user?u=11907933");
        }

        private void Discord_OnClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://discord.gg/zgbrkmA");
        }
    }
}
