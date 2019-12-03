using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class VortexInstaller : AInstaller
    {
        public GameMetaData GameInfo { get; internal set; }

        public override ModManager ModManager => ModManager.Vortex;

        public VortexInstaller(string archive, ModList modList, string outputFolder, string downloadFolder)
            : base(
                  archive: archive,
                  modList: modList,
                  outputFolder: outputFolder,
                  downloadFolder: downloadFolder)
        {
            #if DEBUG
            // TODO: only for testing
            IgnoreMissingFiles = true;
            #endif

            GameInfo = GameRegistry.Games[ModList.GameType];
        }

        protected override bool _Begin(CancellationToken cancel)
        {
            if (cancel.IsCancellationRequested) return false;
            MessageBox.Show(
                "Vortex Support is still experimental and may produce unexpected results. " +
                "If anything fails go to the special vortex support channels on the discord. @erri120#2285 " +
                "for support.", "Warning",
                MessageBoxButton.OK);

            if (cancel.IsCancellationRequested) return false;
            ConfigureProcessor(10, RecommendQueueSize());
            Directory.CreateDirectory(DownloadFolder);

            if (cancel.IsCancellationRequested) return false;
            HashArchives();

            if (cancel.IsCancellationRequested) return false;
            DownloadArchives();

            if (cancel.IsCancellationRequested) return false;
            HashArchives();

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

            PrimeVFS();

            if (cancel.IsCancellationRequested) return false;
            BuildFolderStructure();

            if (cancel.IsCancellationRequested) return false;
            InstallArchives();

            if (cancel.IsCancellationRequested) return false;
            InstallIncludedFiles();

            if (cancel.IsCancellationRequested) return false;
            InstallSteamWorkshopItems();
            //InstallIncludedDownloadMetas();

            Info("Installation complete! You may exit the program.");
            return true;
        }

        private void InstallSteamWorkshopItems()
        {
            //var currentLib = "";
            SteamGame currentSteamGame = null;
            SteamHandler.Instance.Games.Where(g => g.Game == GameInfo.Game).Do(s => currentSteamGame = s);
            /*SteamHandler.Instance.InstallFolders.Where(f => f.Contains(currentSteamGame.InstallDir)).Do(s => currentLib = s);

            var downloadFolder = Path.Combine(currentLib, "workshop", "downloads", currentSteamGame.AppId.ToString());
            var contentFolder = Path.Combine(currentLib, "workshop", "content", currentSteamGame.AppId.ToString());
            */
            if (!ModList.Directives.Any(s => s is SteamMeta))
                return;

            var result = MessageBox.Show("The ModList you are installing requires Steam Workshop Items to exist. " +
                                         "You can check the Workshop Items in the manifest of this ModList. Wabbajack can start Steam for you " +
                                         "and download the Items automatically. Do you want to proceed with this step?",
                "Warning", MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;

            ModList.Directives.OfType<SteamMeta>()
                .PMap(Queue, item =>
                {
                    Status("Extracting Steam meta file to temp folder");
                    var path = Path.Combine(DownloadFolder, $"steamWorkshopItem_{item.ItemID}.meta");
                    if (!File.Exists(path))
                        File.WriteAllBytes(path, LoadBytesFromPath(item.SourceDataID));

                    Status("Downloading Steam Workshop Item through steam cmd");

                    var p = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = System.IO.Path.Combine(SteamHandler.Instance.SteamPath, "steam.exe"),
                            CreateNoWindow = true,
                            Arguments = $"console +workshop_download_item {currentSteamGame.AppId} {item.ItemID}"
                        }
                    };

                    p.Start();
                });
        }

        private void InstallIncludedFiles()
        {
            Info("Writing inline files");
            ModList.Directives.OfType<InlineFile>()
                .PMap(Queue,directive =>
                {
                    if (directive.To.EndsWith(".meta"))
                        return;

                    Status($"Writing included file {directive.To}");
                    var outPath = Path.Combine(OutputFolder, directive.To);
                    if(File.Exists(outPath)) File.Delete(outPath);
                    File.WriteAllBytes(outPath, LoadBytesFromPath(directive.SourceDataID));
                });
        }
    }
}
