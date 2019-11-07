using System.Windows;
using System.Windows.Controls;
using Wabbajack.Lib;

namespace Wabbajack.Views
{
    public partial class StartupView : UserControl
    {
        public StartupView()
        {
            InitializeComponent();
            Banner.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");
        }

        private void Startup_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!(sender is StartupView s)) return;
            MainScrollViewer.VerticalScrollBarVisibility = s.ActualHeight <= 750 ? ScrollBarVisibility.Visible : ScrollBarVisibility.Hidden;
        }
    }
}
