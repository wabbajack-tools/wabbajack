using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.UI;

namespace Wabbajack.Views
{
    public partial class ModListGalleryView : UserControl
    {
        public ModListGalleryView()
        {
            InitializeComponent();
            Banner.Source = UIUtils.BitmapImageFromResource("Wabbajack.Resources.Wabba_Mouth.png");
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
            if (!(sender is Button b)) return;
            if (!(b.DataContext is ModlistMetadata mm)) return;
            var link = mm.Links.Download;

            if (!Directory.Exists(Consts.ModListDownloadFolder))
                Directory.CreateDirectory(Consts.ModListDownloadFolder);
            var dest = Path.Combine(Consts.ModListDownloadFolder, mm.Links.MachineURL + ExtensionManager.Extension);

            var downloadWindow = new DownloadWindow(link, mm.Title, dest);
            downloadWindow.ShowDialog();
        }
    }
}
