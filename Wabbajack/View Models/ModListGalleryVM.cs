using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ReactiveUI;
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

        public IReactiveCommand BackCommand { get; }

        public ModListGalleryVM(MainWindowVM mainWindow)
        {
            BackCommand = ReactiveCommand.Create(() => { mainWindow.CurrentPage = Page.StartUp; });

            if(ModLists.Count >= 1)
                ItemsControlVisibility = Visibility.Visible;
        }
    }
}
