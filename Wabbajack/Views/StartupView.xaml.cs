using System.Windows.Controls;
using Wabbajack.Lib;

namespace Wabbajack.Views
{
    public partial class StartupView : UserControl
    {
        public StartupView()
        {
            InitializeComponent();
            Banner.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.banner_small_dark.png");
        }
    }
}
