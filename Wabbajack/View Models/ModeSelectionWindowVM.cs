using Alphaleonis.Win32.Filesystem;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.UI
{
    public class ModeSelectionWindowVM : ViewModel
    {
        public ObservableCollection<ModlistMetadata> ModLists { get; } = new ObservableCollection<ModlistMetadata>(ModlistMetadata.LoadFromGithub());

        [Reactive]
        public ModlistMetadata SelectedModList { get; set; }

        [Reactive]
        public bool CanInstall { get; set; }

        internal string Download()
        {
            if (!Directory.Exists(Consts.ModListDownloadFolder))
                Directory.CreateDirectory(Consts.ModListDownloadFolder);

            string dest = Path.Combine(Consts.ModListDownloadFolder, SelectedModList.Links.MachineURL + ExtensionManager.Extension);

            var window = new DownloadWindow(SelectedModList.Links.Download, SelectedModList.Title, dest);
            window.ShowDialog();

            if (window.Result == DownloadWindow.WindowResult.Completed)
                return dest;
            return null;
        }
    }
}
