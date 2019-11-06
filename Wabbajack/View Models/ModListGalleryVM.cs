using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.UI;

namespace Wabbajack
{
    public class ModListGalleryVM : ViewModel
    {
        public ObservableCollection<ModlistMetadata> ModLists { get; internal set; } = new ObservableCollection<ModlistMetadata>(ModlistMetadata.LoadFromGithub());

        [Reactive]
        public Visibility ItemsControlVisibility { get; set; }

        public ModListGalleryVM()
        {
            if(ModLists.Count >= 1)
                ItemsControlVisibility = Visibility.Visible;
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
