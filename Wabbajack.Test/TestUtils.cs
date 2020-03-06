using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Test
{
    public class TestUtils : IDisposable
    {
        private static Random _rng = new Random();
        public TestUtils()
        {
            ID = RandomName();
            WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "tmp_data");
        }

        public string WorkingDirectory { get;}
        public string ID { get; }
        public Random RNG => _rng;

        public Game Game { get; set; }

        public string TestFolder => Path.Combine(WorkingDirectory, ID);
        public string GameFolder => Path.Combine(WorkingDirectory, ID, "game_folder");

        public string MO2Folder => Path.Combine(WorkingDirectory, ID, "mo2_folder");
        public string ModsFolder => Path.Combine(MO2Folder, Consts.MO2ModFolderName);
        public string DownloadsFolder => Path.Combine(MO2Folder, "downloads");

        public string InstallFolder => Path.Combine(TestFolder, "installed");

        public HashSet<string> Profiles = new HashSet<string>();

        public List<string> Mods = new List<string>();

        public void Configure()
        {
            File.WriteAllLines(Path.Combine(MO2Folder, "ModOrganizer.ini"), new []
            {
                "[General]",
                $"gameName={Game.MetaData().MO2Name}",
                $"gamePath={GameFolder.Replace("\\", "\\\\")}",
                $"download_directory={DownloadsFolder}"
            });

            Directory.CreateDirectory(DownloadsFolder);
            Directory.CreateDirectory(Path.Combine(GameFolder, "Data"));

            Profiles.Do(profile =>
            {
                File.WriteAllLines(Path.Combine(MO2Folder, "profiles", profile, "modlist.txt"),
                    Mods.Select(s => $"+{s}").ToArray());
            });
        }

        public string AddProfile(string name = null)
        {
            string profile_name = name ?? RandomName();
            Directory.CreateDirectory(Path.Combine(MO2Folder, "profiles", profile_name));
            Profiles.Add(profile_name);
            return profile_name;
        }

        public string AddMod(string name = null)
        {
            lock (this)
            {
                string mod_name = name ?? RandomName();
                var mod_folder = Path.Combine(MO2Folder, Consts.MO2ModFolderName, mod_name);
                Directory.CreateDirectory(mod_folder);
                File.WriteAllText(Path.Combine(mod_folder, "meta.ini"), "[General]");
                Mods.Add(mod_name);
                return mod_name;
            }
        }

        /// <summary>
        /// Adds a file to the given mod with a given path in the mod. Fills it with random data unless
        /// random_fill == 0;
        /// </summary>
        /// <param name="mod_name"></param>
        /// <param name="path"></param>
        /// <param name="random_fill"></param>
        /// <returns></returns>
        public string AddModFile(string mod_name, string path, int random_fill=128)
        {


            var full_path = Path.Combine(ModsFolder, mod_name, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full_path));

            GenerateRandomFileData(full_path, random_fill);
            return full_path;
        }

        public void GenerateRandomFileData(string full_path, int random_fill)
        {
            byte[] bytes = new byte[0];
            if (random_fill != 0)
            {
                bytes = new byte[random_fill];
                RNG.NextBytes(bytes);
            }

            File.WriteAllBytes(full_path, bytes);
        }

        public static byte[] RandomData(int? size = null, int maxSize = 1024)
        {
            if (size == null)
                size = _rng.Next(1, maxSize);
            var arr = new byte[(int) size];
            _rng.NextBytes(arr);
            return arr;
        }

        public void Dispose()
        {
            var exts = new [] {".md", ".exe"};
            Utils.DeleteDirectory((AbsolutePath)Path.Combine(WorkingDirectory, ID)).Wait();
            Profiles.Do(p =>
            {
                foreach (var ext in exts) {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), p + ext);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            });
        }

        /// <summary>
        /// Returns a random string name (with spaces)
        /// </summary>
        public string RandomName()
        {
            return Guid.NewGuid().ToString();
        }

        public string AddManualDownload(Dictionary<string, byte[]> contents)
        {
            var name = RandomName() + ".zip";

            using(FileStream fs = new FileStream(Path.Combine(DownloadsFolder, name), FileMode.Create))
            using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                contents.Do(kv =>
                {
                    var entry = archive.CreateEntry(kv.Key);
                    using (var os = entry.Open())
                        os.Write(kv.Value, 0, kv.Value.Length);
                });
            }

            File.WriteAllLines(Path.Combine(DownloadsFolder, name + Consts.MetaFileExtension),
                
                new string[]
                {
                    "[General]",
                    "manualURL=<TESTING>"
                });

            return name;
        }

        public void VerifyInstalledFile(string mod, string file)
        {
            var src = Path.Combine(MO2Folder, Consts.MO2ModFolderName, mod, file);
            Assert.IsTrue(File.Exists(src), src);

            var dest = Path.Combine(InstallFolder, Consts.MO2ModFolderName, mod, file);
            Assert.IsTrue(File.Exists(dest), dest);

            var src_data = File.ReadAllBytes(src);
            var dest_data = File.ReadAllBytes(dest);

            Assert.AreEqual(src_data.Length, dest_data.Length);

            for(int x = 0; x < src_data.Length; x++)
            {
                if (src_data[x] != dest_data[x])
                    Assert.Fail($"Index {x} of {mod}\\{file} are not the same");
            }
        }
        
        public void VerifyInstalledGameFile(string file)
        {
            var src = Path.Combine(GameFolder, file);
            Assert.IsTrue(File.Exists(src), src);

            var dest = Path.Combine(InstallFolder, Consts.GameFolderFilesDir, file);
            Assert.IsTrue(File.Exists(dest), dest);

            var src_data = File.ReadAllBytes(src);
            var dest_data = File.ReadAllBytes(dest);

            Assert.AreEqual(src_data.Length, dest_data.Length);

            for(int x = 0; x < src_data.Length; x++)
            {
                if (src_data[x] != dest_data[x])
                    Assert.Fail($"Index {x} of {Consts.GameFolderFilesDir}\\{file} are not the same");
            }
        }
        public string PathOfInstalledFile(string mod, string file)
        {
            return Path.Combine(InstallFolder, Consts.MO2ModFolderName, mod, file);
        }

        public void VerifyAllFiles()
        {
            var skip_files = new HashSet<string> {"portable.txt"};
            foreach (var dest_file in Directory.EnumerateFiles(InstallFolder, "*", DirectoryEnumerationOptions.Recursive))
            {
                var rel_file = dest_file.RelativeTo(InstallFolder);
                if (rel_file.StartsWith(Consts.LOOTFolderFilesDir) || rel_file.StartsWith(Consts.GameFolderFilesDir))
                    continue;
                
                if (!skip_files.Contains(rel_file)) 
                    Assert.IsTrue(File.Exists(Path.Combine(MO2Folder, rel_file)), $"Only in Destination: {rel_file}");
            }

            var skip_extensions = new HashSet<string> {".txt", ".ini"};

            foreach (var src_file in Directory.EnumerateFiles(MO2Folder, "*", DirectoryEnumerationOptions.Recursive))
            {
                var rel_file = src_file.RelativeTo(MO2Folder);

                if (rel_file.StartsWith("downloads\\"))
                    continue;

                var dest_file = Path.Combine(InstallFolder, rel_file);
                Assert.IsTrue(File.Exists(dest_file), $"Only in Source: {rel_file}");

                var fi_src = new FileInfo(src_file);
                var fi_dest = new FileInfo(dest_file);

                if (!skip_extensions.Contains(Path.GetExtension(src_file)))
                {
                    Assert.AreEqual(fi_src.Length, fi_dest.Length, $"Differing sizes {rel_file}");
                    Assert.AreEqual(src_file.FileHash(), dest_file.FileHash(), $"Differing content hash {rel_file}");
                }
            }
        }

        public string AddGameFile(string path, int i)
        {
            var full_path = Path.Combine(GameFolder, path);
            var dir = Path.GetDirectoryName(full_path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            GenerateRandomFileData(full_path, i);
            return full_path;
        }

        public static object RandomeOne(params object[] opts)
        {
            return opts[_rng.Next(0, opts.Length)];
        }
    }
}
