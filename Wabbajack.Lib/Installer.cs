using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class Installer
    {
        private string _downloadsFolder;

        public Installer(string archive, ModList mod_list, string output_folder)
        {
            ModListArchive = archive;
            Outputfolder = output_folder;
            ModList = mod_list;
        }

        public VirtualFileSystem VFS => VirtualFileSystem.VFS;

        public string Outputfolder { get; }

        public string DownloadFolder
        {
            get => _downloadsFolder ?? Path.Combine(Outputfolder, "downloads");
            set => _downloadsFolder = value;
        }

        public string ModListArchive { get; }
        public ModList ModList { get; }
        public Dictionary<string, string> HashedArchives { get; private set; }

        public bool IgnoreMissingFiles { get; internal set; }
        public string GameFolder { get; set; }

        public void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            WorkQueue.Report(msg, 0);
        }

        public void Status(string msg, int progress)
        {
            WorkQueue.Report(msg, progress);
        }

        private void Error(string msg)
        {
            Utils.Log(msg);
            throw new Exception(msg);
        }

        public byte[] LoadBytesFromPath(string path)
        {
            using (var fs = new FileStream(ModListArchive, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(path);
                using (var e = entry.Open())
                    e.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static ModList LoadFromFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var entry = ar.GetEntry("modlist");
                if (entry == null)
                {
                    entry = ar.GetEntry("modlist.json");
                    using (var e = entry.Open())
                        return e.FromJSON<ModList>();
                }
                using (var e = entry.Open())
                    return e.FromCERAS<ModList>(ref CerasConfig.Config);
            }
        }

        public void Install()
        {
            var game = GameRegistry.Games[ModList.GameType];

            if (GameFolder == null)
                GameFolder = game.GameLocation;

            if (GameFolder == null)
            {
                MessageBox.Show(
                    $"In order to do a proper install Wabbajack needs to know where your {game.MO2Name} folder resides. We tried looking the" +
                    "game location up in the windows registry but were unable to find it, please make sure you launch the game once before running this installer. ",
                    "Could not find game location", MessageBoxButton.OK);
                Utils.Log("Exiting because we couldn't find the game folder.");
                return;
            }

            ValidateGameESMs();
            ValidateModlist.RunValidation(ModList);

            VirtualFileSystem.Clean();
            Directory.CreateDirectory(Outputfolder);
            Directory.CreateDirectory(DownloadFolder);

            if (Directory.Exists(Path.Combine(Outputfolder, "mods")))
            {
                if (MessageBox.Show(
                        "There already appears to be a Mod Organizer 2 install in this folder, are you sure you wish to continue" +
                        " with installation? If you do, you may render both your existing install and the new modlist inoperable.",
                        "Existing MO2 installation in install folder",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation) == MessageBoxResult.No)
                {
                    Utils.Log("Existing installation at the request of the user, existing mods folder found.");
                    return;
                }
            }


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
            InctallIncludedDownloadMetas();
            BuildBSAs();

            zEditIntegration.GenerateMerges(this);

            Info("Installation complete! You may exit the program.");
            // Removed until we decide if we want this functionality
            // Nexus devs weren't sure this was a good idea, I (halgari) agree.
            //AskToEndorse();
        }

        private void InctallIncludedDownloadMetas()
        {
            ModList.Directives
                   .OfType<ArchiveMeta>()
                   .PMap(directive =>
                   {
                       Status($"Writing included .meta file {directive.To}");
                       var out_path = Path.Combine(DownloadFolder, directive.To);
                       if (File.Exists(out_path)) File.Delete(out_path);
                       File.WriteAllBytes(out_path, LoadBytesFromPath(directive.SourceDataID));
                   });
        }

        private void ValidateGameESMs()
        {
            foreach (var esm in ModList.Directives.OfType<CleanedESM>().ToList())
            {
                var filename = Path.GetFileName(esm.To);
                var game_file = Path.Combine(GameFolder, "Data", filename);
                Utils.Log($"Validating {filename}");
                var hash = game_file.FileHash();
                if (hash != esm.SourceESMHash)
                {
                    Utils.Error("Game ESM hash doesn't match, is the ESM already cleaned? Please verify your local game files.");
                }
            }
        }

        private void AskToEndorse()
        {
            var mods = ModList.Archives
                .Select(m => m.State)
                .OfType<NexusDownloader.State>()
                .GroupBy(f => (f.GameName, f.ModID))
                .Select(mod => mod.First())
                .ToArray();

            var result = MessageBox.Show(
                $"Installation has completed, but you have installed {mods.Length} from the Nexus, would you like to" +
                " endorse these mods to show support to the authors? It will only take a few moments.", "Endorse Mods?",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Shuffle mods so that if we hit a API limit we don't always miss the same mods
            var r = new Random();
            for (var i = 0; i < mods.Length; i++)
            {
                var a = r.Next(mods.Length);
                var b = r.Next(mods.Length);
                var tmp = mods[a];
                mods[a] = mods[b];
                mods[b] = tmp;
            }

            mods.PMap(mod =>
            {
                var er = new NexusApiClient().EndorseMod(mod);
                Utils.Log($"Endorsed {mod.GameName} - {mod.ModID} - Result: {er.message}");
            });
            Info("Done! You may now exit the application!");
        }

        private bool LocateGameFolder()
        {
            var fs = UIUtils.ShowFolderSelectionDialog("Please locate your game installation path");
            if (fs != null)
            {
                GameFolder = fs;
                return true;
            }

            return false;
        }


        /// <summary>
        ///     We don't want to make the installer index all the archives, that's just a waste of time, so instead
        ///     we'll pass just enough information to VFS to let it know about the files we have.
        /// </summary>
        private void PrimeVFS()
        {
            HashedArchives.Do(a => VFS.AddKnown(new VirtualFile
            {
                Paths = new[] { a.Value },
                Hash = a.Key
            }));
            VFS.RefreshIndexes();


            ModList.Directives
                .OfType<FromArchive>()
                .Do(f =>
                {
                    var updated_path = new string[f.ArchiveHashPath.Length];
                    f.ArchiveHashPath.CopyTo(updated_path, 0);
                    updated_path[0] = VFS.HashIndex[updated_path[0]].Where(e => e.IsConcrete).First().FullPath;
                    VFS.AddKnown(new VirtualFile { Paths = updated_path });
                });

            VFS.BackfillMissing();
        }

        private void BuildBSAs()
        {
            var bsas = ModList.Directives.OfType<CreateBSA>().ToList();
            Info($"Building {bsas.Count} bsa files");

            bsas.Do(bsa =>
            {
                Status($"Building {bsa.To}");
                var source_dir = Path.Combine(Outputfolder, Consts.BSACreationDir, bsa.TempID);

                using (var a = bsa.State.MakeBuilder())
                {
                    bsa.FileStates.PMap(state =>
                    {
                        Status($"Adding {state.Path} to BSA");
                        using (var fs = File.OpenRead(Path.Combine(source_dir, state.Path)))
                        {
                            a.AddFile(state, fs);
                        }
                    });

                    Info($"Writing {bsa.To}");
                    a.Build(Path.Combine(Outputfolder, bsa.To));
                }
            });


            var bsa_dir = Path.Combine(Outputfolder, Consts.BSACreationDir);
            if (Directory.Exists(bsa_dir))
            {
                Info($"Removing temp folder {Consts.BSACreationDir}");
                VirtualFileSystem.DeleteDirectory(bsa_dir);
            }
        }

        private void InstallIncludedFiles()
        {
            Info("Writing inline files");
            ModList.Directives
                .OfType<InlineFile>()
                .PMap(directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var out_path = Path.Combine(Outputfolder, directive.To);
                    if (File.Exists(out_path)) File.Delete(out_path);
                    if (directive is RemappedInlineFile)
                        WriteRemappedFile((RemappedInlineFile)directive);
                    else if (directive is CleanedESM)
                        GenerateCleanedESM((CleanedESM)directive);
                    else
                        File.WriteAllBytes(out_path, LoadBytesFromPath(directive.SourceDataID));
                });
        }

        private void GenerateCleanedESM(CleanedESM directive)
        {
            var filename = Path.GetFileName(directive.To);
            var game_file = Path.Combine(GameFolder, "Data", filename);
            Info($"Generating cleaned ESM for {filename}");
            if (!File.Exists(game_file)) throw new InvalidDataException($"Missing {filename} at {game_file}");
            Status($"Hashing game version of {filename}");
            var sha = game_file.FileHash();
            if (sha != directive.SourceESMHash)
                throw new InvalidDataException(
                    $"Cannot patch {filename} from the game folder hashes don't match have you already cleaned the file?");

            var patch_data = LoadBytesFromPath(directive.SourceDataID);
            var to_file = Path.Combine(Outputfolder, directive.To);
            Status($"Patching {filename}");
            using (var output = File.OpenWrite(to_file))
            using (var input = File.OpenRead(game_file))
            {
                BSDiff.Apply(input, () => new MemoryStream(patch_data), output);
            }
        }

        private void WriteRemappedFile(RemappedInlineFile directive)
        {
            var data = Encoding.UTF8.GetString(LoadBytesFromPath(directive.SourceDataID));

            data = data.Replace(Consts.GAME_PATH_MAGIC_BACK, GameFolder);
            data = data.Replace(Consts.GAME_PATH_MAGIC_DOUBLE_BACK, GameFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.GAME_PATH_MAGIC_FORWARD, GameFolder.Replace("\\", "/"));

            data = data.Replace(Consts.MO2_PATH_MAGIC_BACK, Outputfolder);
            data = data.Replace(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, Outputfolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.MO2_PATH_MAGIC_FORWARD, Outputfolder.Replace("\\", "/"));

            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_BACK, DownloadFolder);
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK, DownloadFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_FORWARD, DownloadFolder.Replace("\\", "/"));

            File.WriteAllText(Path.Combine(Outputfolder, directive.To), data);
        }

        private void BuildFolderStructure()
        {
            Info("Building Folder Structure");
            ModList.Directives
                .Select(d => Path.Combine(Outputfolder, Path.GetDirectoryName(d.To)))
                .ToHashSet()
                .Do(f =>
                {
                    if (Directory.Exists(f)) return;
                    Directory.CreateDirectory(f);
                });
        }

        private void InstallArchives()
        {
            Info("Installing Archives");
            Info("Grouping Install Files");
            var grouped = ModList.Directives
                .OfType<FromArchive>()
                .GroupBy(e => e.ArchiveHashPath[0])
                .ToDictionary(k => k.Key);
            var archives = ModList.Archives
                .Select(a => new { Archive = a, AbsolutePath = HashedArchives.GetOrDefault(a.Hash) })
                .Where(a => a.AbsolutePath != null)
                .ToList();

            Info("Installing Archives");
            archives.PMap(a => InstallArchive(a.Archive, a.AbsolutePath, grouped[a.Archive.Hash]));
        }

        private void InstallArchive(Archive archive, string absolutePath, IGrouping<string, FromArchive> grouping)
        {
            Status($"Extracting {archive.Name}");

            var vfiles = grouping.Select(g =>
            {
                var file = VFS.FileForArchiveHashPath(g.ArchiveHashPath);
                g.FromFile = file;
                return g;
            }).ToList();

            var on_finish = VFS.Stage(vfiles.Select(f => f.FromFile).Distinct());


            Status($"Copying files for {archive.Name}");

            vfiles.DoIndexed((idx, file) =>
            {
                Utils.Status("Installing files", idx * 100 / vfiles.Count);
                var dest = Path.Combine(Outputfolder, file.To);
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Copy(file.FromFile.StagedPath, dest);
            });

            Status("Unstaging files");
            on_finish();

            // Now patch all the files from this archive
            foreach (var to_patch in grouping.OfType<PatchedFromArchive>())
                using (var patch_stream = new MemoryStream())
                {
                    Status($"Patching {Path.GetFileName(to_patch.To)}");
                    // Read in the patch data

                    var patch_data = LoadBytesFromPath(to_patch.PatchID);

                    var to_file = Path.Combine(Outputfolder, to_patch.To);
                    var old_data = new MemoryStream(File.ReadAllBytes(to_file));

                    // Remove the file we're about to patch
                    File.Delete(to_file);

                    // Patch it
                    using (var out_stream = File.OpenWrite(to_file))
                    {
                        BSDiff.Apply(old_data, () => new MemoryStream(patch_data), out_stream);
                    }

                    Status($"Verifying Patch {Path.GetFileName(to_patch.To)}");
                    var result_sha = to_file.FileHash();
                    if (result_sha != to_patch.Hash)
                        throw new InvalidDataException($"Invalid Hash for {to_patch.To} after patching");
                }
        }

        private void DownloadArchives()
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            Info($"Missing {missing.Count} archives");

            Info("Getting Nexus API Key, if a browser appears, please accept");

            var dispatchers = ModList.Archives.Select(m => m.State.GetDownloader()).Distinct();

            foreach (var dispatcher in dispatchers)
                dispatcher.Prepare();

            DownloadMissingArchives(missing);
        }

        private void DownloadMissingArchives(List<Archive> missing, bool download = true)
        {
            missing.PMap(archive =>
            {
                Info($"Downloading {archive.Name}");
                var output_path = Path.Combine(DownloadFolder, archive.Name);

                if (download)
                    if (output_path.FileExists())
                        File.Delete(output_path);

                return DownloadArchive(archive, download);
            });
        }

        public bool DownloadArchive(Archive archive, bool download)
        {
            try
            {
                archive.State.Download(archive, Path.Combine(DownloadFolder, archive.Name));
            }
            catch (Exception ex)
            {
                Utils.Log($"Download error for file {archive.Name}");
                Utils.Log(ex.ToString());
                return false;
            }

            return false;
        }

        private void HashArchives()
        {
            HashedArchives = Directory.EnumerateFiles(DownloadFolder)
                .Where(e => !e.EndsWith(".sha"))
                .PMap(e => (HashArchive(e), e))
                .OrderByDescending(e => File.GetLastWriteTime(e.Item2))
                .GroupBy(e => e.Item1)
                .Select(e => e.First())
                .ToDictionary(e => e.Item1, e => e.Item2);
        }

        private string HashArchive(string e)
        {
            var cache = e + ".sha";
            if (cache.FileExists() && new FileInfo(cache).LastWriteTime >= new FileInfo(e).LastWriteTime)
                return File.ReadAllText(cache);

            Status($"Hashing {Path.GetFileName(e)}");
            File.WriteAllText(cache, e.FileHash());
            return HashArchive(e);
        }
    }
}
