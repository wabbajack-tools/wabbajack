using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using DirectoryInfo = Alphaleonis.Win32.Filesystem.DirectoryInfo;
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

        protected override bool _Begin()
        {
            MessageBox.Show(
                "Vortex Support is still experimental and may produce unexpected results. " +
                "If anything fails go to the special vortex support channels on the discord. @erri120#2285 " +
                "for support.", "Warning",
                MessageBoxButton.OK);

            ConfigureProcessor(10, RecommendQueueSize());
            Directory.CreateDirectory(DownloadFolder);

            HashArchives();
            DownloadArchives();
            HashArchives();

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

            BuildFolderStructure();
            InstallArchives();
            InstallIncludedFiles();
            InstallManualGameFiles();
            InstallSteamWorkshopItems();
            //InstallIncludedDownloadMetas();

            Info("Installation complete! You may exit the program.");
            return true;
        }

        private void InstallManualGameFiles()
        {
            if (!ModList.Directives.Any(d => d.To.StartsWith(Consts.ManualGameFilesDir)))
                return;

            var result = MessageBox.Show("Some mods from this ModList must be installed directly into " +
                                             "the game folder. Do you want to do this manually or do you want Wabbajack " +
                                             "to do this for you?", "Question", MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;

            var manualFilesDir = Path.Combine(OutputFolder, Consts.ManualGameFilesDir);

            var gameFolder = GameInfo.GameLocation();

            Info($"Copying files from {manualFilesDir} " +
                 $"to the game folder at {gameFolder}");

            if (!Directory.Exists(manualFilesDir))
            {
                Info($"{manualFilesDir} does not exist!");
                return;
            }

            Directory.EnumerateDirectories(manualFilesDir).PMap(Queue, dir =>
            {
                var dirInfo = new DirectoryInfo(dir);
                dirInfo.GetDirectories("*", SearchOption.AllDirectories).Do(d =>
                {
                    var destPath = d.FullName.Replace(dir, gameFolder);
                    Status($"Creating directory {destPath}");
                    Directory.CreateDirectory(destPath);
                });

                dirInfo.GetFiles("*", SearchOption.AllDirectories).Do(f =>
                {
                    var destPath = f.FullName.Replace(dir, gameFolder);
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
