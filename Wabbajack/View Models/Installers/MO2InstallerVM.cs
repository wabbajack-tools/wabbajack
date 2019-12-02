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
    public class MO2InstallerVM : ViewModel, ISubInstallerVM
    {
        public IReactiveCommand BeginCommand { get; }

        [Reactive]
        public AInstaller ActiveInstallation { get; private set; }

        public MO2InstallerVM(InstallerVM installerVM)
        {
            BeginCommand = ReactiveCommand.CreateFromTask(
                canExecute: Observable.CombineLatest(
                        installerVM.WhenAny(x => x.Location.InError),
                        installerVM.WhenAny(x => x.DownloadLocation.InError),
                        resultSelector: (loc, download) =>
                        {
                            return !loc && !download;
                        })
                    .ObserveOnGuiThread(),
                execute: async () =>
                {
                    AInstaller installer;

                    try
                    {
                        installer = new MO2Installer(
                            archive: installerVM.ModListPath.TargetPath,
                            modList: installerVM.ModList.SourceModList,
                            outputFolder: installerVM.Location.TargetPath,
                            downloadFolder: installerVM.DownloadLocation.TargetPath);
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
