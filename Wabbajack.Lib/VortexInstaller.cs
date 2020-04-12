using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Common.StoreHandlers;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using DirectoryInfo = Alphaleonis.Win32.Filesystem.DirectoryInfo;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
#nullable disable

namespace Wabbajack.Lib
{
    public class VortexInstaller : AInstaller
    {
        public GameMetaData GameInfo { get; internal set; }

        public override ModManager ModManager => ModManager.Vortex;

        public VortexInstaller(AbsolutePath archive, ModList modList, AbsolutePath outputFolder, AbsolutePath downloadFolder, SystemParameters parameters)
            : base(
                  archive: archive,
                  modList: modList,
                  outputFolder: outputFolder,
                  downloadFolder: downloadFolder,
                  parameters: parameters,
                  steps: 10)
        {
            #if DEBUG
            // TODO: only for testing
            IgnoreMissingFiles = true;
            #endif

            GameInfo = ModList.GameType.MetaData();
        }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return false;
            var metric = Metrics.Send(Metrics.BeginInstall, ModList.Name);
            var result = await Utils.Log(new YesNoIntervention(
                "Vortex Support is still experimental and may produce unexpected results. " +
                "If anything fails please go to the special Vortex support channels on the Wabbajack Discord and contact @erri120#2285 " +
                "for support.", "Continue with experimental feature?")).Task;
            if (result == ConfirmationIntervention.Choice.Abort)
            {
                Utils.Log("Exiting at request of user");
                return false;
            }

            if (cancel.IsCancellationRequested) return false;
            Queue.SetActiveThreadsObservable(ConstructDynamicNumThreads(await RecommendQueueSize()));
            DownloadFolder.CreateDirectory();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Archives");
            await HashArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Downloading Missing Archives");
            await DownloadArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Remaining Archives");
            await HashArchives();

            if (cancel.IsCancellationRequested) return false;
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            if (missing.Count > 0)
            {
                foreach (var a in missing)
                    Info($"Unable to download {a.Name}");
                if (IgnoreMissingFiles)
                    Info("Missing some archives, but continuing anyways at the request of the user");
                else
                    Error("Cannot continue, was unable to download one or more archives");
            }

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Priming VFS");
            await PrimeVFS();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Building Folder Structure");
            BuildFolderStructure();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Archives");
            await InstallArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Included files");
            await InstallIncludedFiles();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Manual files");
            await InstallManualGameFiles();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing SteamWorkshopItems");
            await InstallSteamWorkshopItems();

            //InstallIncludedDownloadMetas();
            var metric2 = Metrics.Send(Metrics.FinishInstall, ModList.Name);
            UpdateTracker.NextStep("Installation complete! You may exit the program.");
            return true;
        }

        private async Task InstallManualGameFiles()
        {
            if (!ModList.Directives.Any(d => d.To.StartsWith(Consts.ManualGameFilesDir)))
                return;

            var result = await Utils.Log(new YesNoIntervention("Some mods from this ModList must be installed directly into " +
                                             "the game folder. Do you want to do this manually or do you want Wabbajack " +
                                             "to do this for you?", "Move mods into game folder?")).Task;

            if (result != ConfirmationIntervention.Choice.Continue)
                return;

            var manualFilesDir = OutputFolder.Combine(Consts.ManualGameFilesDir);

            var gameFolder = GameInfo.TryGetGameLocation();

            Info($"Copying files from {manualFilesDir} " +
                 $"to the game folder at {gameFolder}");

            if (!manualFilesDir.Exists)
            {
                Info($"{manualFilesDir} does not exist!");
                return;
            }

            /* TODO FIX THIS
            await manualFilesDir.EnumerateFiles().PMap(Queue, dir =>
            {
                dirInfo.GetDirectories("*", SearchOption.AllDirectories).Do(d =>
                {
                    var destPath = d.FullName.Replace(manualFilesDir, gameFolder);
                    Status($"Creating directory {destPath}");
                    Directory.CreateDirectory(destPath);
                });

                dirInfo.GetFiles("*", SearchOption.AllDirectories).Do(f =>
                {
                    var destPath = f.FullName.Replace(manualFilesDir, gameFolder);
                    Status($"Copying file {f.FullName} to {destPath}");
                    try
                    {
                        File.Copy(f.FullName, destPath);
                    }
                    catch (Exception)
                    {
                        Info($"Could not copy file {f.FullName} to {destPath}. The file may already exist, skipping...");
                    }
                });
            });
            
            */
        }

        private async Task InstallSteamWorkshopItems()
        {
            //var currentLib = "";
            SteamGame currentSteamGame = null;
            StoreHandler.Instance.SteamHandler.Games.Where(g => g.Game == GameInfo.Game).Do(s => currentSteamGame = (SteamGame)s);
            /*SteamHandler.Instance.InstallFolders.Where(f => f.Contains(currentSteamGame.InstallDir)).Do(s => currentLib = s);

            var downloadFolder = Path.Combine(currentLib, "workshop", "downloads", currentSteamGame.AppId.ToString());
            var contentFolder = Path.Combine(currentLib, "workshop", "content", currentSteamGame.AppId.ToString());
            */
            if (!ModList.Directives.Any(s => s is SteamMeta))
                return;

            var result = await Utils.Log(new YesNoIntervention(
                "The ModList you are installing requires Steam Workshop items to exist. " +
                "You can check the Workshop Items in the manifest of this ModList. Wabbajack can start Steam for you " +
                "and download the Items automatically. Do you want to proceed with this step?",
                "Download Steam Workshop Items?")).Task;

            if (result != ConfirmationIntervention.Choice.Continue)
                return;

            await ModList.Directives.OfType<SteamMeta>()
                .PMap(Queue, async item =>
                {
                    Status("Extracting Steam meta file to temp folder");
                    var path = DownloadFolder.Combine($"steamWorkshopItem_{item.ItemID}.meta");
                    if (!path.Exists)
                        await path.WriteAllBytesAsync(await LoadBytesFromPath(item.SourceDataID));

                    Status("Downloading Steam Workshop Item through steam cmd");

                    var p = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(StoreHandler.Instance.SteamHandler.SteamPath, "steam.exe"),
                            CreateNoWindow = true,
                            Arguments = $"console +workshop_download_item {currentSteamGame.ID} {currentSteamGame.ID}"
                        }
                    };

                    p.Start();
                });
        }

        private async Task InstallIncludedFiles()
        {
            Info("Writing inline files");
            await ModList.Directives.OfType<InlineFile>()
                .PMap(Queue,async directive =>
                {
                    if (directive.To.Extension == Consts.MetaFileExtension)
                        return;

                    Info($"Writing included file {directive.To}");
                    var outPath = OutputFolder.Combine(directive.To);
                    outPath.Delete();
                    await outPath.WriteAllBytesAsync(await LoadBytesFromPath(directive.SourceDataID));
                });
        }
    }
}
