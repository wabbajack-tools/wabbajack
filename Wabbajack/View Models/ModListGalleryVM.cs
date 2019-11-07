using System.Collections.ObjectModel;
using System.Windows;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

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
