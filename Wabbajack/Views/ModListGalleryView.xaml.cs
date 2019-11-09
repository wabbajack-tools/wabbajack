using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Views
{
    public partial class ModListGalleryView : UserControl
    {
        public ModListGalleryView()
        {
            InitializeComponent();
        }
        
        public void Info_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button b)) return;
            if (!(b.DataContext is ModlistMetadata mm)) return;
            var link = mm.Links.MachineURL;
            Process.Start($"https://www.wabbajack.org/modlist/{link}");
        }

        public void Download_OnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            /* unsure about this since the downloader changed



             if (!(sender is Button b)) return;
            if (!(b.DataContext is ModlistMetadata mm)) return;
            var link = mm.Links.Download;

            if (!Directory.Exists(Consts.ModListDownloadFolder))
                Directory.CreateDirectory(Consts.ModListDownloadFolder);
            var dest = Path.Combine(Consts.ModListDownloadFolder, mm.Links.MachineURL + ExtensionManager.Extension);

            var downloadWindow = new DownloadWindow(link, mm.Title, mm.,dest);
            downloadWindow.ShowDialog();*/
        }

        private void Tile_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Tile t)) return;
            if (!t.IsFocused) return;
            if (!t.IsMouseOver) return;
            if (!(t.DataContext is ModlistMetadata mm)) return;
            var link = mm.Links.MachineURL;
            Process.Start($"https://www.wabbajack.org/modlist/{link}");
        }
    }
}
