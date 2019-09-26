using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CG.Web.MegaApiClient;
using Compression.BSA;
using K4os.Compression.LZ4.Streams;
using VFS;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack
{
    public class Installer
    {
        private string _downloadsFolder;

        public Installer(ModList mod_list, string output_folder)
        {
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

        public ModList ModList { get; }
        public Dictionary<string, string> HashedArchives { get; private set; }

        public string NexusAPIKey { get; set; }
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

        public void Install()
        {
            VirtualFileSystem.Clean();
            Directory.CreateDirectory(Outputfolder);
            Directory.CreateDirectory(DownloadFolder);

            if (Directory.Exists(Path.Combine(Outputfolder, "mods")))
            {
                if (MessageBox.Show(
                        "There already appears to be a Mod Organize 2 install in this folder, are you sure you wish to continue" +
                        " with installation? If you do, you may render both your existing install and the new modlist inoperable.",
                        "Existing MO2 installation in install folder",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation) == MessageBoxResult.No)
                {
                    Utils.Log("Existing installation at the request of the user, existing mods folder found.");
                    return;
                }
            }

            if (GameFolder == null)
            {
                MessageBox.Show(
                    "In order to do a proper install Wabbajack needs to know where your game folder resides. This is most likely " +
                    "somewhere in one of your Steam folders. Please select this folder on the next screen." +
                    "Note: This is not the install location where Mod Organizer 2 will be installed. ",
                    "Select your Game Folder", MessageBoxButton.OK);
                if (!LocateGameFolder())
                {
                    Info("Stopping installation because game folder was not selected");
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
            BuildBSAs();

            Info("Installation complete! You may exit the program.");
            // Removed until we decide if we want this functionality
            // Nexus devs weren't sure this was a good idea, I (halgari) agree.
            //AskToEndorse();
        }

        private void AskToEndorse()
        {
            var mods = ModList.Archives
                .OfType<NexusMod>()
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
                var er = NexusAPI.EndorseMod(mod, NexusAPIKey);
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
                Paths = new[] {a.Value},
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
                    VFS.AddKnown(new VirtualFile {Paths = updated_path});
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
                var source_files = Directory.EnumerateFiles(source_dir, "*", SearchOption.AllDirectories)
                    .Select(e => e.Substring(source_dir.Length + 1))
                    .ToList();

                if (source_files.Count > 0)
                    using (var a = new BSABuilder())
                    {
                        //a.Create(Path.Combine(Outputfolder, bsa.To), (bsa_archive_type_t)bsa.Type, entries);
                        a.HeaderType = (VersionType) bsa.Type;
                        a.FileFlags = (FileFlags) bsa.FileFlags;
                        a.ArchiveFlags = (ArchiveFlags) bsa.ArchiveFlags;

                        source_files.PMap(f =>
                        {
                            Status($"Adding {f} to BSA");
                            using (var fs = File.OpenRead(Path.Combine(source_dir, f)))
                            {
                                a.AddFile(f, fs);
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
                        WriteRemappedFile((RemappedInlineFile) directive);
                    else if (directive is CleanedESM)
                        GenerateCleanedESM((CleanedESM) directive);
                    else
                        File.WriteAllBytes(out_path, directive.SourceData.FromBase64());
                });
        }

        private void GenerateCleanedESM(CleanedESM directive)
        {
            var filename = Path.GetFileName(directive.To);
            var game_file = Path.Combine(GameFolder, "Data", filename);
            Info($"Generating cleaned ESM for {filename}");
            if (!File.Exists(game_file)) throw new InvalidDataException($"Missing {filename} at {game_file}");
            Status($"Hashing game version of {filename}");
            var sha = game_file.FileSHA256();
            if (sha != directive.SourceESMHash)
                throw new InvalidDataException(
                    $"Cannot patch {filename} from the game folder hashes don't match have you already cleaned the file?");

            var patch_data = directive.SourceData.FromBase64();
            var to_file = Path.Combine(Outputfolder, directive.To);
            Status($"Patching {filename}");
            using (var output = File.OpenWrite(to_file))
            {
                BSDiff.Apply(File.OpenRead(game_file), () => new MemoryStream(patch_data), output);
            }
        }

        private void WriteRemappedFile(RemappedInlineFile directive)
        {
            var data = Encoding.UTF8.GetString(directive.SourceData.FromBase64());

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
                .Select(a => new {Archive = a, AbsolutePath = HashedArchives.GetOrDefault(a.Hash)})
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
                File.Copy(file.FromFile.StagedPath, Path.Combine(Outputfolder, file.To));
            });

            Status("Unstaging files");
            on_finish();

            // Now patch all the files from this archive
            foreach (var to_patch in grouping.OfType<PatchedFromArchive>())
                using (var patch_stream = new MemoryStream())
                {
                    Status($"Patching {Path.GetFileName(to_patch.To)}");
                    // Read in the patch data

                    var patch_data = to_patch.Patch;

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
                    var result_sha = to_file.FileSHA256();
                    if (result_sha != to_patch.Hash)
                        throw new InvalidDataException($"Invalid Hash for {to_patch.To} after patching");
                }
        }

        private void DownloadArchives()
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            Info($"Missing {missing.Count} archives");

            Info("Getting Nexus API Key, if a browser appears, please accept");
            if (ModList.Archives.OfType<NexusMod>().Any())
            {
                NexusAPIKey = NexusAPI.GetNexusAPIKey();

                var user_status = NexusAPI.GetUserStatus(NexusAPIKey);

                if (!user_status.is_premium)
                {
                    Info(
                        $"Automated installs with Wabbajack requires a premium nexus account. {user_status.name} is not a premium account");
                    return;
                }
            }

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
                switch (archive)
                {
                    case NexusMod a:
                        string url;
                        try
                        {
                            url = NexusAPI.GetNexusDownloadLink(a, NexusAPIKey, !download);
                            if (!download) return true;
                        }
                        catch (Exception ex)
                        {
                            Info($"{a.Name} - Error Getting Nexus Download URL - {ex.Message}");
                            return false;
                        }

                        Info($"Downloading Nexus Archive - {archive.Name} - {a.GameName} - {a.ModID} - {a.FileID}");
                        DownloadURLDirect(archive, url);
                        return true;
                    case MEGAArchive a:
                        return DownloadMegaArchive(a, download);
                    case GoogleDriveMod a:
                        return DownloadGoogleDriveArchive(a, download);
                    case MODDBArchive a:
                        return DownloadModDBArchive(archive, (archive as MODDBArchive).URL, download);
                    case MediaFireArchive a:
                        return false;
                    //return DownloadMediaFireArchive(archive, a.URL, download);
                    case DirectURLArchive a:
                        return DownloadURLDirect(archive, a.URL, headers: a.Headers, download: download);
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"Download error for file {archive.Name}");
                Utils.Log(ex.ToString());
                return false;
            }

            return false;
        }

        private void DownloadMediaFireArchive(Archive a, string url)
        {
            var client = new HttpClient();
            var result = client.GetStringSync(url);
            var regex = new Regex("(?<= href =\\\").*\\.mediafire\\.com.*(?=\\\")");
            var confirm = regex.Match(result);
            DownloadURLDirect(a, confirm.ToString(), client);
        }

        private bool DownloadMegaArchive(MEGAArchive m, bool download)
        {
            var client = new MegaApiClient();
            Status("Logging into MEGA (as anonymous)");
            client.LoginAnonymous();
            var file_link = new Uri(m.URL);
            var node = client.GetNodeFromLink(file_link);
            if (!download) return true;
            Status($"Downloading MEGA file: {m.Name}");

            var output_path = Path.Combine(DownloadFolder, m.Name);
            client.DownloadFile(file_link, output_path);
            return true;
        }

        private bool DownloadGoogleDriveArchive(GoogleDriveMod a, bool download)
        {
            var initial_url = $"https://drive.google.com/uc?id={a.Id}&export=download";
            var client = new HttpClient();
            var result = client.GetStringSync(initial_url);
            var regex = new Regex("(?<=/uc\\?export=download&amp;confirm=).*(?=;id=)");
            var confirm = regex.Match(result);
            return DownloadURLDirect(a, $"https://drive.google.com/uc?export=download&confirm={confirm}&id={a.Id}",
                client, download);
        }

        private bool DownloadModDBArchive(Archive archive, string url, bool download)
        {
            var client = new HttpClient();
            var result = client.GetStringSync(url);
            var regex = new Regex("https:\\/\\/www\\.moddb\\.com\\/downloads\\/mirror\\/.*(?=\\\")");
            var match = regex.Match(result);
            return DownloadURLDirect(archive, match.Value, download: download);
        }

        private bool DownloadURLDirect(Archive archive, string url, HttpClient client = null, bool download = true,
            List<string> headers = null)
        {
            try
            {
                if (client == null)
                {
                    client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", Consts.UserAgent);
                }

                if (headers != null)
                    foreach (var header in headers)
                    {
                        var idx = header.IndexOf(':');
                        var k = header.Substring(0, idx);
                        var v = header.Substring(idx + 1);
                        client.DefaultRequestHeaders.Add(k, v);
                    }

                long total_read = 0;
                var buffer_size = 1024 * 32;

                var response = client.GetSync(url);
                var stream = response.Content.ReadAsStreamAsync();
                try
                {
                    stream.Wait();
                }
                catch (Exception ex)
                {
                }

                ;
                if (stream.IsFaulted)
                {
                    Info($"While downloading {url} - {stream.Exception.ExceptionToString()}");
                    return false;
                }

                if (!download)
                    return true;

                var header_var = "1";
                if (response.Content.Headers.Contains("Content-Length"))
                    header_var = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();

                var content_size = header_var != null ? long.Parse(header_var) : 1;

                var output_path = Path.Combine(DownloadFolder, archive.Name);
                ;

                using (var webs = stream.Result)
                using (var fs = File.OpenWrite(output_path))
                {
                    var buffer = new byte[buffer_size];
                    while (true)
                    {
                        var read = webs.Read(buffer, 0, buffer_size);
                        if (read == 0) break;
                        Status("Downloading {archive.Name}", (int)(total_read * 100 / content_size));

                        fs.Write(buffer, 0, read);
                        total_read += read;
                    }
                }

                Status($"Hashing {archive.Name}");
                HashArchive(output_path);
                return true;
            }
            catch (Exception ex)
            {
                Info($"{archive.Name} - Error downloading from: {url}");
                return false;
            }
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
            File.WriteAllText(cache, e.FileSHA256());
            return HashArchive(e);
        }

        public static ModList CheckForModList()
        {
            Utils.Log("Looking for attached modlist");
            using (var s = File.OpenRead(Assembly.GetExecutingAssembly().Location))
            {
                var magic_bytes = Encoding.ASCII.GetBytes(Consts.ModListMagic);
                s.Position = s.Length - magic_bytes.Length;
                using (var br = new BinaryReader(s))
                {
                    var bytes = br.ReadBytes(magic_bytes.Length);
                    var magic = Encoding.ASCII.GetString(bytes);

                    if (magic != Consts.ModListMagic) return null;
                  
                    s.Position = s.Length - magic_bytes.Length - 8;
                    var start_pos = br.ReadInt64();
                    s.Position = start_pos;
                    Utils.Log("Modlist found, loading...");
                    using (var dc = LZ4Stream.Decode(br.BaseStream, leaveOpen: true))
                    {
                        IFormatter formatter = new BinaryFormatter();
                        var list = formatter.Deserialize(dc);
                        Utils.Log("Modlist loaded.");
                        return (ModList) list;
                    }
                }
            }
        }
    }
}