using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack
{
    public class Installer
    {
        public Installer(ModList mod_list, string output_folder, Action<string> log_fn)
        {
            Outputfolder = output_folder;
            ModList = mod_list;
            Log_Fn = log_fn;
        }

        public string Outputfolder { get; }
        public string DownloadFolder
        {
            get
            {
                return Path.Combine(Outputfolder, "downloads");
            }
        }
        public ModList ModList { get; }
        public Action<string> Log_Fn { get; }
        public Dictionary<string, string> HashedArchives { get; private set; }
        public string NexusAPIKey { get; private set; }

        public void Info(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            Log_Fn(msg);
        }

        public void Status(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            WorkQueue.Report(msg, 0);
        }

        public void Status(int progress, string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            WorkQueue.Report(msg, progress);
        }
        private void Error(string msg, params object[] args)
        {
            if (args.Length > 0)
                msg = String.Format(msg, args);
            Log_Fn(msg);
            throw new Exception(msg);
        }

        public void Install()
        {
            Directory.CreateDirectory(Outputfolder);
            Directory.CreateDirectory(DownloadFolder);

            HashArchives();
            DownloadArchives();
            HashArchives();

            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            if (missing.Count > 0)
            {
                foreach (var a in missing)
                    Info("Unable to download {0}", a.Name);
                Error("Cannot continue, was unable to download one or more archives");
            }
            BuildFolderStructure();
            InstallArchives();

            Info("Installation complete! You may exit the program.");
        }

        private void BuildFolderStructure()
        {
            Info("Building Folder Structure");
            ModList.Directives
                   .Select(d => Path.Combine(Outputfolder, Path.GetDirectoryName(d.To)))
                   .ToHashSet()
                   .Do(f => {
                       if (Directory.Exists(f)) return;
                       Directory.CreateDirectory(f);
                   });
        }

        private void InstallArchives()
        {
            Info("Installing Archives");
            var grouped = ModList.Directives
                                 .OfType<FromArchive>()
                                 .GroupBy(e => e.ArchiveHash)
                                 .ToDictionary(k => k.Key);
            var archives = ModList.Archives
                                  .Select(a => new { Archive = a, AbsolutePath = HashedArchives[a.Hash] })
                                  .ToList();

            archives.PMap(a => InstallArchive(a.Archive, a.AbsolutePath, grouped[a.Archive.Hash]));

        }

        private void InstallArchive(Archive archive, string absolutePath, IGrouping<string, FromArchive> grouping)
        {
            Status("Extracting {0}", archive.Name);
            var files = grouping.GroupBy(e => e.From)
                                .ToDictionary(e => e.Key);

            using (var a = new ArchiveFile(absolutePath))
            {
                a.Extract(entry =>
                {
                    if (files.TryGetValue(entry.FileName, out var directives))
                    {
                        var directive = directives.First();
                        var absolute = Path.Combine(Outputfolder, directive.To);
                        if (absolute.FileExists()) File.Delete(absolute);
                        return File.OpenWrite(absolute);
                    }
                    return null;
                });
            }

            Status("Copying duplicated files for {0}", archive.Name);

            foreach (var dups in files.Where(e => e.Value.Count() > 1).Select(v => v.Value))
            {
                var ffrom = dups.First();
                var from_path = Path.Combine(Outputfolder, ffrom.To);
                foreach (var to in dups.Skip(1))
                {
                    var to_path = Path.Combine(Outputfolder, to.To);
                    if (to_path.FileExists()) File.Delete(to_path);
                    File.Copy(from_path, to_path);
                }
            };

            // Now patch all the files from this archive
            foreach (var to_patch in grouping.OfType<PatchedFromArchive>())
            {
                using (var patch_stream = new MemoryStream())
                {
                    Status("Patching {0}", Path.GetFileName(to_patch.To));
                    // Read in the patch data

                    var patch_data = to_patch.Patch.FromBase64();

                    var to_file = Path.Combine(Outputfolder, to_patch.To);
                    MemoryStream old_data = new MemoryStream(File.ReadAllBytes(to_file));

                    // Patch it
                    using (var out_stream = File.OpenWrite(to_file))
                    {
                        BSDiff.Apply(old_data, () => new MemoryStream(patch_data), out_stream);
                    }
                }

            }
        }

        private void DownloadArchives()
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            Info("Missing {0} archives", missing.Count);

            Info("Getting Nexus API Key, if a browser appears, please accept");
            NexusAPIKey = NexusAPI.GetNexusAPIKey();

            DownloadMissingArchives(missing);
        }

        private void DownloadMissingArchives(List<Archive> missing)
        {
            missing.PMap(archive =>
            {
                if (archive is NexusMod)
                {
                    var url = NexusAPI.GetNexusDownloadLink(archive as NexusMod, NexusAPIKey);
                    DownloadURLDirect(archive, url);
                }
                else if (archive is DirectURLArchive)
                {
                    DownloadURLDirect(archive, (archive as DirectURLArchive).URL);
                }
                else
                {

                }
            });
        }

        private void DownloadURLDirect(Archive archive, string url)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", Consts.UserAgent);
            long total_read = 0;
            int buffer_size = 1024 * 32;

            var response = client.GetSync(url);
            var stream = response.Content.ReadAsStreamAsync();
            stream.Wait();

            var header = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();
            long content_size = header != null ? long.Parse(header) : 1;

            var output_path = Path.Combine(DownloadFolder, archive.Name);

            if (output_path.FileExists())
                File.Delete(output_path);

            using (var webs = stream.Result)
            using (var fs = File.OpenWrite(output_path))
            {
                var buffer = new byte[buffer_size];
                while (true)
                {
                    var read = webs.Read(buffer, 0, buffer_size);
                    if (read == 0) break;
                    Status((int)(total_read * 100 / content_size), "Downloading {0}", archive.Name);

                    fs.Write(buffer, 0, read);
                    total_read += read;

                }
            }
            Status("Hashing {0}", archive.Name);
            HashArchive(output_path);
        }

        private object GetNexusAPIKey()
        {
            throw new NotImplementedException();
        }

        private void HashArchives()
        {
            HashedArchives = Directory.EnumerateFiles(DownloadFolder)
                                      .Where(e => Consts.SupportedArchives.Contains(Path.GetExtension(e)))
                                      .PMap(e => (HashArchive(e), e))
                                      .ToDictionary(e => e.Item1, e => e.Item2);

        }

        private string HashArchive(string e)
        {
            var cache = e + ".sha";
            if (cache.FileExists() && new FileInfo(cache).LastWriteTime >= new FileInfo(e).LastWriteTime)
                return File.ReadAllText(cache);

            Status("Hashing {0}", Path.GetFileName(e));
            File.WriteAllText(cache, Utils.FileSHA256(e));
            return HashArchive(e);

        }

    }
}
