using System.Windows;
using System.Windows.Controls;

namespace Wabbajack.Views
{
    public partial class ModListGalleryView : UserControl
    {
        private ModListGalleryVM _vm;

        public ModListGalleryView()
        {
            InitializeComponent();
            _vm = new ModListGalleryVM();
            DataContext = _vm;
        }

        private void Info_OnClick(object sender, RoutedEventArgs e)
        {
            _vm.Info_OnClick(sender, e);
        }

        private void Download_OnClick(object sender, RoutedEventArgs e)
        {
            _vm.Download_OnClick(sender, e);
        }
    }
}
