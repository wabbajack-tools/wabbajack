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

namespace Wabbajack
{
    public class VortexInstallerVM : ViewModel, ISubInstallerVM
    {
        public InstallerVM Parent { get; }

        public IReactiveCommand BeginCommand { get; }

        [Reactive]
        public AInstaller ActiveInstallation { get; private set; }

        private readonly ObservableAsPropertyHelper<string> _DownloadLocation;
        public string DownloadLocation => _DownloadLocation.Value;

        private readonly ObservableAsPropertyHelper<string> _StagingLocation;
        public string StagingLocation => _StagingLocation.Value;

        private readonly ObservableAsPropertyHelper<Game> _TargetGame;
        public Game TargetGame => _TargetGame.Value;

        public VortexInstallerVM(InstallerVM installerVM)
        {
            Parent = installerVM;

            _TargetGame = installerVM.WhenAny(x => x.ModList.SourceModList.GameType)
                .ToProperty(this, nameof(TargetGame));

            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: Observable.CombineLatest(
                    this.WhenAny(x => x.TargetGame)
                        .Select(game => VortexCompiler.IsActiveVortexGame(game)),
                    installerVM.WhenAny(x => x.ModListLocation.InError),
                    resultSelector: (isVortexGame, modListErr) => isVortexGame && !modListErr),
                execute: async () =>
                {
                    AInstaller installer;

                    try
                    {
                        var download = VortexCompiler.RetrieveDownloadLocation(TargetGame);
                        var staging = VortexCompiler.RetrieveStagingLocation(TargetGame);
                        installer = new VortexInstaller(
                            archive: installerVM.ModListLocation.TargetPath,
                            modList: installerVM.ModList.SourceModList,
                            outputFolder: staging,
                            downloadFolder: download);
                    }
                    catch (Exception ex)
                    {
                        while (ex.InnerException != null) ex = ex.InnerException;
                        Utils.Log(ex.StackTrace);
                        Utils.Log(ex.ToString());
                        Utils.Log($"{ex.Message} - Can't continue");
                        ActiveInstallation = null;
                        return;
                    }

                    await Task.Run(async () =>
                    {
                        IDisposable subscription = null;
                        try
                        {
                            var workTask = installer.Begin();
                            ActiveInstallation = installer;
                            await workTask;
                        }
                        catch (Exception ex)
                        {
                            while (ex.InnerException != null) ex = ex.InnerException;
                            Utils.Log(ex.StackTrace);
                            Utils.Log(ex.ToString());
                            Utils.Log($"{ex.Message} - Can't continue");
                        }
                        finally
                        {
                            // Dispose of CPU tracking systems
                            subscription?.Dispose();
                            ActiveInstallation = null;
                        }
                    });
                });
        }

        public void Unload()
        {
        }
    }
}
