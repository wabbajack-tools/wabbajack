using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for ModlistPropertiesWindow.xaml
    /// </summary>
    public partial class ModlistPropertiesWindow : Window
    {
        public ModlistPropertiesWindow()
        {
            InitializeComponent();
            var bannerImage = UIUtils.BitmapImageFromResource("Wabbajack.banner.png");
            SplashScreenProperty.Source = bannerImage;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //Hide();
        }

        private void SetSplashScreen_Click(object sender, RoutedEventArgs e)
        {
            var file = UIUtils.OpenFileDialog("Banner image|*.png");
            if(file != null)
            {
                SplashScreenProperty.Source = new BitmapImage(new Uri(file));
            }
        }
    }
}
