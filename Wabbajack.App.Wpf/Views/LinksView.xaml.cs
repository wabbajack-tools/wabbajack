using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Wabbajack.Common;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for LinksView.xaml
    /// </summary>
    public partial class LinksView : UserControl
    {
        public LinksView()
        {
            InitializeComponent();
        }

        private void GitHub_Click(object sender, RoutedEventArgs e)
        {
            UIUtils.OpenWebsite(new Uri("https://github.com/wabbajack-tools/wabbajack"));
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            UIUtils.OpenWebsite(new Uri("https://discord.gg/wabbajack"));
        }

        private void Patreon_Click(object sender, RoutedEventArgs e)
        {
            UIUtils.OpenWebsite(new Uri("https://www.patreon.com/user?u=11907933"));
        }
    }
}
