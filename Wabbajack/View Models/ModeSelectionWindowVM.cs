using Alphaleonis.Win32.Filesystem;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
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

        private readonly ObservableAsPropertyHelper<bool> _CanInstall;
        public bool CanInstall => _CanInstall.Value;

        public ModeSelectionWindowVM()
        {
            this._CanInstall = this.WhenAny(x => x.SelectedModList)
                .Select(x => x != null)
                .ToProperty(this, nameof(this.CanInstall));
        }

        internal string Download()
        {
            if (!Directory.Exists(Consts.ModListDownloadFolder))
                Directory.CreateDirectory(Consts.ModListDownloadFolder);

            string dest = Path.Combine(Consts.ModListDownloadFolder, SelectedModList.Links.MachineURL + ExtensionManager.Extension);

            var window = new DownloadWindow(SelectedModList.Links.Download, 
                                           SelectedModList.Title, 
                                               SelectedModList.Links.DownloadMetadata?.Size ?? 0,
                                               dest);
            window.ShowDialog();

            if (window.Result == DownloadWindow.WindowResult.Completed)
                return dest;
            return null;
        }
    }
}
