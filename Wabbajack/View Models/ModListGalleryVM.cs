using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DynamicData.Binding;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

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
            throw new NotImplementedException();
        }
    }
}
