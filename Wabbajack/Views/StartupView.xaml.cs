using System.Windows.Controls;
using Wabbajack.Lib;

namespace Wabbajack.Views
{
    public partial class StartupView : UserControl
    {
        public StartupView()
        {
            InitializeComponent();
            //Banner.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.banner_small_dark.png");
            Banner.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");
            MO2Image.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.MO2Button.png");
            VortexImage.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.VortexButton.png");
        }
    }
}
