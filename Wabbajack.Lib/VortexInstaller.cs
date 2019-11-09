using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using VFS;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class VortexInstaller
    {
        public string ModListArchive { get; }
        public ModList ModList { get; }
        public Dictionary<string, string> HashedArchives { get; private set; }

        public GameMetaData GameInfo { get; internal set; }

        public string VortexFolder { get; set; }
        public string StagingFolder { get; set; }
        public string DownloadFolder { get; set; }

        public VirtualFileSystem VFS => VirtualFileSystem.VFS;

        public bool IgnoreMissingFiles { get; internal set; }

        public VortexInstaller(string archive, ModList modList)
        {
            ModListArchive = archive;
            ModList = modList;

            // TODO: only for testing
            IgnoreMissingFiles = true;

            GameInfo = GameRegistry.Games[ModList.GameType];

            VortexFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vortex");
            StagingFolder = Path.Combine(VortexFolder, GameInfo.NexusName, "mods");
            DownloadFolder = Path.Combine(VortexFolder, "downloads", GameInfo.NexusName);
        }

        public void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            WorkQueue.Report(msg, 0);
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
            Directory.CreateDirectory(DownloadFolder);

            VirtualFileSystem.Clean();

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
            //InctallIncludedDownloadMetas();

            Info("Installation complete! You may exit the program.");
        }

        private void BuildFolderStructure()
        {
            Info("Building Folder Structure");
            ModList.Directives
                .OfType<FromArchive>()
                .Select(d => Path.Combine(StagingFolder, Path.GetDirectoryName(d.To)))
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

            var vFiles = grouping.Select(g =>
            {
                var file = VFS.FileForArchiveHashPath(g.ArchiveHashPath);
                g.FromFile = file;
                return g;
            }).ToList();

            var onFinish = VFS.Stage(vFiles.Select(f => f.FromFile).Distinct());

            Status($"Copying files for {archive.Name}");

            void CopyFile(string from, string to, bool useMove)
            {
                if(File.Exists(to))
                    File.Delete(to);
                if (useMove)
                    File.Move(from, to);
                else
                    File.Copy(from, to);
            }

            vFiles.GroupBy(f => f.FromFile)
                .DoIndexed((idx, group) =>
                {
                    Utils.Status("Installing files", idx * 100 / vFiles.Count);
                    var firstDest = Path.Combine(StagingFolder, group.First().To);
                    CopyFile(group.Key.StagedPath, firstDest, true);

                    foreach (var copy in group.Skip(1))
                    {
                        var nextDest = Path.Combine(StagingFolder, copy.To);
                        CopyFile(firstDest, nextDest, false);
                    }
                });

            Status("Unstaging files");
            onFinish();
        }

        private void InstallIncludedFiles()
        {
            Info("Writing inline files");
            ModList.Directives.OfType<InlineFile>()
                .PMap(directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var outPath = Path.Combine(StagingFolder, directive.To);
                    if(File.Exists(outPath)) File.Delete(outPath);
                    File.WriteAllBytes(outPath, LoadBytesFromPath(directive.SourceDataID));
                });
        }

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

        private void DownloadArchives()
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            Info($"Missing {missing.Count} archives");

            Info("Getting Nexus API Key, if a browser appears, please accept");

            var dispatchers = missing.Select(m => m.State.GetDownloader()).Distinct();

            foreach (var dispatcher in dispatchers)
                dispatcher.Prepare();
            
            DownloadMissingArchives(missing);
        }

        private void DownloadMissingArchives(List<Archive> missing, bool download = true)
        {
            if (download)
            {
                foreach (var a in missing.Where(a => a.State.GetType() == typeof(ManualDownloader.State)))
                {
                    var output_path = Path.Combine(DownloadFolder, a.Name);
                    a.State.Download(a, output_path);
                }
            }

            missing.Where(a => a.State.GetType() != typeof(ManualDownloader.State))
                .PMap(archive =>
                {
                    Info($"Downloading {archive.Name}");
                    var output_path = Path.Combine(DownloadFolder, archive.Name);

                    if (!download) return DownloadArchive(archive, download);
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
