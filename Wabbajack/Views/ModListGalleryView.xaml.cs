using System.Windows.Controls;

namespace Wabbajack.Views
{
    public partial class ModListGalleryView : UserControl
    {
        public ModListGalleryView()
        {
            InitializeComponent();
            DataContext = new ModListGalleryVM();
        }
    }
}
