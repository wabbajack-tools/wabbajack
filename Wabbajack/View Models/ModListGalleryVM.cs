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

        public string TestImage => ModLists[0].Links.ImageUri;
        public string TestTitle => $"{ModLists[0].Title} by {ModLists[0].Author}";
        public string TestDescription => ModLists[0].Description;
        public string TestGame => ModLists[0].Game.ToString();

        public ModListGalleryVM()
        {

        }
    }
}
