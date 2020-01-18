using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Util;

namespace Wabbajack
{
    public class VortexInstallerVM : ViewModel, ISubInstallerVM
    {
        public InstallerVM Parent { get; }

        [Reactive]
        public AInstaller ActiveInstallation { get; private set; }

        private readonly ObservableAsPropertyHelper<string> _DownloadLocation;
        public string DownloadLocation => _DownloadLocation.Value;

        private readonly ObservableAsPropertyHelper<string> _StagingLocation;
        public string StagingLocation => _StagingLocation.Value;

        private readonly ObservableAsPropertyHelper<Game> _TargetGame;
        public Game TargetGame => _TargetGame.Value;

        public bool SupportsAfterInstallNavigation => false;

        public int ConfigVisualVerticalOffset => 0;

        public IObservable<bool> CanInstall { get; }

        public VortexInstallerVM(InstallerVM installerVM)
        {
            Parent = installerVM;

            _TargetGame = installerVM.WhenAny(x => x.ModList.SourceModList.GameType)
                .ToGuiProperty(this, nameof(TargetGame));

            CanInstall = Observable.CombineLatest(
                this.WhenAny(x => x.TargetGame)
                    .Select(game => VortexCompiler.IsActiveVortexGame(game)),
                installerVM.WhenAny(x => x.ModListLocation.InError),
                resultSelector: (isVortexGame, modListErr) => isVortexGame && !modListErr);
        }

        public void Unload()
        {
        }

        public void AfterInstallNavigation()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Install()
        {
            AInstaller installer;

            var download = VortexCompiler.RetrieveDownloadLocation(TargetGame);
            var staging = VortexCompiler.RetrieveStagingLocation(TargetGame);
            using (installer = new VortexInstaller(
                archive: Parent.ModListLocation.TargetPath,
                modList: Parent.ModList.SourceModList,
                outputFolder: staging,
                downloadFolder: download,
                parameters: SystemParametersConstructor.Create()))
            {
                Parent.MWVM.Settings.Performance.AttachToBatchProcessor(installer);

                return await Task.Run(async () =>
                {
                    try
                    {
                        var workTask = installer.Begin();
                        ActiveInstallation = installer;
                        return await workTask;
                    }
                    finally
                    {
                        ActiveInstallation = null;
                    }
                });
            }
        }
    }
}
