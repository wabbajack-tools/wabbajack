using System.Linq;
using Wabbajack.Common;
using Wabbajack.VirtualFileSystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class VortexInstaller : AInstaller
    {
        public GameMetaData GameInfo { get; internal set; }

        public VortexInstaller(string archive, ModList modList)
        {
            UpdateTracker = new StatusUpdateTracker(10);
            VFS = new Context(Queue) {UpdateTracker = UpdateTracker};
            ModManager = ModManager.Vortex;
            ModListArchive = archive;
            ModList = modList;

            // TODO: only for testing
            IgnoreMissingFiles = true;

            GameInfo = GameRegistry.Games[ModList.GameType];
        }

        public override void Install()
        {
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
            //InstallIncludedDownloadMetas();

            Info("Installation complete! You may exit the program.");
        }

        private void InstallIncludedFiles()
        {
            Info("Writing inline files");
            ModList.Directives.OfType<InlineFile>()
                .PMap(Queue,directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var outPath = Path.Combine(OutputFolder, directive.To);
                    if(File.Exists(outPath)) File.Delete(outPath);
                    File.WriteAllBytes(outPath, LoadBytesFromPath(directive.SourceDataID));
                });
        }
    }
}
