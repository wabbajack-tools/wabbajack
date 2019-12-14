using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
            Process.Start("https://github.com/wabbajack-tools/wabbajack");
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/wabbajack");
        }

        private void Patreon_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://www.patreon.com/user?u=11907933");
        }
    }
}
