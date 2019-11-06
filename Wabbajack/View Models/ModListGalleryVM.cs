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

        private MainWindowVM _mainWindow;

        [Reactive]
        public Visibility ItemsControlVisibility { get; set; }

        public IReactiveCommand BackCommand { get; }

        public ModListGalleryVM(MainWindowVM mainWindow)
        {
            _mainWindow = mainWindow;

            BackCommand = ReactiveCommand.Create(() => { mainWindow.Page = 0; });

            if(ModLists.Count >= 1)
                ItemsControlVisibility = Visibility.Visible;
        }
    }
}
