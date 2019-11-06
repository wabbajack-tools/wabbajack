using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Binding;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack
{
    public class ModListGalleryVM : ViewModel
    {
        
        public ObservableCollection<ModlistMetadata> ModLists { get; } = new ObservableCollectionExtended<ModlistMetadata>(ModlistMetadata.LoadFromGithub());

        public ModListGalleryVM()
        {

        }
    }
}
