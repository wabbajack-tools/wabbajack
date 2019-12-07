using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public abstract class AInstaller : ABatchProcessor
    {
        public bool IgnoreMissingFiles { get; internal set; } = false;

        public string OutputFolder { get; private set; }
        public string DownloadFolder { get; private set; }

        public abstract ModManager ModManager { get; }

        public string ModListArchive { get; private set; }
        public ModList ModList { get; private set; }
        public Dictionary<string, string> HashedArchives { get; set; }

        public AInstaller(string archive, ModList modList, string outputFolder, string downloadFolder)
        {
            ModList = modList;
            ModListArchive = archive;
            OutputFolder = outputFolder;
            DownloadFolder = downloadFolder;
        }

        public void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            Queue.Report(msg, 0);
        }

        public void Error(string msg)
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

        /// <summary>
        ///     We don't want to make the installer index all the archives, that's just a waste of time, so instead
        ///     we'll pass just enough information to VFS to let it know about the files we have.
        /// </summary>
        public void PrimeVFS()
        {
            VFS.AddKnown(HashedArchives.Select(a => new KnownFile
            {
                Paths = new[] { a.Value },
                Hash = a.Key
            }));

            
            VFS.AddKnown(
                ModList.Directives
                    .OfType<FromArchive>()
                    .Select(f => new KnownFile { Paths = f.ArchiveHashPath}));

            VFS.BackfillMissing();
        }

        public void BuildFolderStructure()
        {
            Info("Building Folder Structure");
            ModList.Directives
                .Select(d => Path.Combine(OutputFolder, Path.GetDirectoryName(d.To)))
                .ToHashSet()
                .Do(f =>
                {
                    if (Directory.Exists(f)) return;
                    Directory.CreateDirectory(f);
                });
        }

        public async Task InstallArchives()
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
            await archives.PMap(Queue, UpdateTracker,a => InstallArchive(a.Archive, a.AbsolutePath, grouped[a.Archive.Hash]));
        }

        private async Task InstallArchive(Archive archive, string absolutePath, IGrouping<string, FromArchive> grouping)
        {
            Status($"Extracting {archive.Name}");

            List<FromArchive> vFiles = grouping.Select(g =>
            {
                var file = VFS.Index.FileForArchiveHashPath(g.ArchiveHashPath);
                g.FromFile = file;
                return g;
            }).ToList();

            var onFinish = await VFS.Stage(vFiles.Select(f => f.FromFile).Distinct());


            Status($"Copying files for {archive.Name}");

            void CopyFile(string from, string to, bool useMove)
            {
                if (File.Exists(to))
                {
                    var fi = new FileInfo(to);
                    if (fi.IsReadOnly)
                        fi.IsReadOnly = false;
                    File.Delete(to);
                }

                if (File.Exists(from))
                {
                    var fi = new FileInfo(from);
                    if (fi.IsReadOnly)
                        fi.IsReadOnly = false;
                }


                if (useMove)
                    File.Move(from, to);
                else
                    File.Copy(from, to);
                // If we don't do this, the file will use the last-modified date of the file when it was compressed
                // into an archive, which isn't really what we want in the case of files installed archives
                File.SetLastWriteTime(to, DateTime.Now);
            }

            vFiles.GroupBy(f => f.FromFile)
                  .DoIndexed((idx, group) =>
            {
                Utils.Status("Installing files", idx * 100 / vFiles.Count);
                var firstDest = Path.Combine(OutputFolder, group.First().To);
                CopyFile(group.Key.StagedPath, firstDest, true);
                
                foreach (var copy in group.Skip(1))
                {
                    var nextDest = Path.Combine(OutputFolder, copy.To);
                    CopyFile(firstDest, nextDest, false);
                }

            });

            Status("Unstaging files");
            onFinish();

            // Now patch all the files from this archive
            foreach (var toPatch in grouping.OfType<PatchedFromArchive>())
                using (var patchStream = new MemoryStream())
                {
                    Status($"Patching {Path.GetFileName(toPatch.To)}");
                    // Read in the patch data

                    byte[] patchData = LoadBytesFromPath(toPatch.PatchID);

                    var toFile = Path.Combine(OutputFolder, toPatch.To);
                    var oldData = new MemoryStream(File.ReadAllBytes(toFile));

                    // Remove the file we're about to patch
                    File.Delete(toFile);

                    // Patch it
                    using (var outStream = File.OpenWrite(toFile))
                    {
                        BSDiff.Apply(oldData, () => new MemoryStream(patchData), outStream);
                    }

                    Status($"Verifying Patch {Path.GetFileName(toPatch.To)}");
                    var resultSha = toFile.FileHash();
                    if (resultSha != toPatch.Hash)
                        throw new InvalidDataException($"Invalid Hash for {toPatch.To} after patching");
                }
        }

        public async Task DownloadArchives()
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            Info($"Missing {missing.Count} archives");

            Info("Getting Nexus API Key, if a browser appears, please accept");

            var dispatchers = missing.Select(m => m.State.GetDownloader()).Distinct();

            await Task.WhenAll(dispatchers.Select(d => d.Prepare()));
            
            await DownloadMissingArchives(missing);
        }

        private async Task DownloadMissingArchives(List<Archive> missing, bool download = true)
        {
            if (download)
            {
                foreach (var a in missing.Where(a => a.State.GetType() == typeof(ManualDownloader.State)))
                {
                    var outputPath = Path.Combine(DownloadFolder, a.Name);
                    await a.State.Download(a, outputPath);
                }
            }

            await missing.Where(a => a.State.GetType() != typeof(ManualDownloader.State))
                .PMap(Queue, async archive =>
                {
                    Info($"Downloading {archive.Name}");
                    var outputPath = Path.Combine(DownloadFolder, archive.Name);

                    if (download)
                        if (outputPath.FileExists())
                            File.Delete(outputPath);

                    return await DownloadArchive(archive, download);
                });
        }

        public async Task<bool> DownloadArchive(Archive archive, bool download)
        {
            try
            {
                var path = Path.Combine(DownloadFolder, archive.Name);
                await archive.State.Download(archive, path);
                path.FileHashCached();

            }
            catch (Exception ex)
            {
                Utils.Log($"Download error for file {archive.Name}");
                Utils.Log(ex.ToString());
                return false;
            }

            return false;
        }

        public async Task HashArchives()
        {
            var hashResults = await Directory.EnumerateFiles(DownloadFolder)
                .Where(e => !e.EndsWith(Consts.HashFileExtension))
                .PMap(Queue, e => (e.FileHashCached(), e));
            HashedArchives = hashResults
                .OrderByDescending(e => File.GetLastWriteTime(e.Item2))
                .GroupBy(e => e.Item1)
                .Select(e => e.First())
                .ToDictionary(e => e.Item1, e => e.Item2);
        }

        /// <summary>
        /// Disabled
        /// </summary>
        public void ValidateFreeSpace()
        {
            return;
            // Disabled, caused more problems than it was worth.
            /* 
                DiskSpaceInfo DriveInfo(string path)
            {
                return Volume.GetDiskFreeSpace(Volume.GetUniqueVolumeNameForPath(path));
            }

            var paths = new[] {(OutputFolder, ModList.InstallSize),
                               (DownloadFolder, ModList.DownloadSize),
                               (Directory.GetCurrentDirectory(), ModList.ScratchSpaceSize)};
            paths.GroupBy(f => DriveInfo(f.Item1).DriveName)
                .Do(g =>
                {
                    var required = g.Sum(i => i.Item2);
                    var contains = g.Sum(folder =>
                        Directory.EnumerateFiles(folder.Item1, "*", DirectoryEnumerationOptions.Recursive)
                            .Sum(file => new FileInfo(file).Length));
                    var available = DriveInfo(g.Key).FreeBytesAvailable;
                    if (required - contains > available)
                        throw new NotEnoughDiskSpaceException(
                            $"This modlist requires {required.ToFileSizeString()} on {g.Key} but only {available.ToFileSizeString()} is available.");
                });
            */
        }

        public async Task<int> RecommendQueueSize()
        {
            var output_size = await RecommendQueueSize(OutputFolder);
            var download_size = await RecommendQueueSize(DownloadFolder);
            var scratch_size = await RecommendQueueSize(Directory.GetCurrentDirectory());
            var result =  Math.Min(output_size, Math.Min(download_size, scratch_size));
            Utils.Log($"Recommending a queue size of {result} based on disk performance and number of cores");
            return result;
        }


        /// <summary>
        /// The user may already have some files in the OutputFolder. If so we can go through these and
        /// figure out which need to be updated, deleted, or left alone
        /// </summary>
        public async Task OptimizeModlist()
        {
            Utils.Log("Optimizing Modlist directives");
            var indexed = ModList.Directives.ToDictionary(d => d.To);

            UpdateTracker.NextStep("Looking for files to delete");
            await Directory.EnumerateFiles(OutputFolder, "*", DirectoryEnumerationOptions.Recursive)
                .PMap(Queue, UpdateTracker, f =>
                {
                    var relative_to = f.RelativeTo(OutputFolder);
                    Utils.Status($"Checking if modlist file {relative_to}");
                    if (indexed.ContainsKey(relative_to) || f.StartsWith(DownloadFolder + Path.DirectorySeparator))
                    {
                        return;
                    }

                    Utils.Log($"Deleting {relative_to} it's not part of this modlist");
                    File.Delete(f);
                });

            UpdateTracker.NextStep("Looking for unmodified files");
            (await indexed.Values.PMap(Queue, UpdateTracker, d =>
            {
                // Bit backwards, but we want to return null for 
                // all files we *want* installed. We return the files
                // to remove from the install list.
                Status($"Optimizing {d.To}");
                var path = Path.Combine(OutputFolder, d.To);
                if (!File.Exists(path)) return null;

                var fi = new FileInfo(path);
                if (fi.Length != d.Size) return null;
                
                return path.FileHash() == d.Hash ? d : null;
            }))
              .Where(d => d != null)
              .Do(d => indexed.Remove(d.To));

            UpdateTracker.NextStep("Updating Modlist");
            Utils.Log($"Optimized {ModList.Directives.Count} directives to {indexed.Count} required");
            var requiredArchives = indexed.Values.OfType<FromArchive>()
                .GroupBy(d => d.ArchiveHashPath[0])
                .Select(d => d.Key)
                .ToHashSet();
            
            ModList.Archives = ModList.Archives.Where(a => requiredArchives.Contains(a.Hash)).ToList();
            ModList.Directives = indexed.Values.ToList();

        }
    }

    public class NotEnoughDiskSpaceException : Exception
    {
        public NotEnoughDiskSpaceException(string s) : base(s)
        {
        }
    }
}
