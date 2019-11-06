using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
