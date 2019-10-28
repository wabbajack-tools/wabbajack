using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Test
{
    public class TestUtils : IDisposable
    {

        public TestUtils()
        {
            RNG = new Random();
            ID = RandomName();
            WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "tmp_data");
        }

        public string WorkingDirectory { get;}
        public string ID { get; }
        public Random RNG { get; }

        public string GameName { get; set; }

        public string TestFolder => Path.Combine(WorkingDirectory, ID);
        public string GameFolder => Path.Combine(WorkingDirectory, ID, "game_folder");

        public string MO2Folder => Path.Combine(WorkingDirectory, ID, "mo2_folder");
        public string ModsFolder => Path.Combine(MO2Folder, "mods");
        public string DownloadsFolder => Path.Combine(MO2Folder, "downloads");

        public string InstallFolder => Path.Combine(TestFolder, "installed");


        public void Configure()
        {
            File.WriteAllLines(Path.Combine(MO2Folder, "ModOrganizer.ini"), new []
            {
                "[General]",
                $"gameName={GameName}",
                $"gamePath={GameFolder.Replace("\\", "\\\\")}",
            });

            Directory.CreateDirectory(DownloadsFolder);
            Directory.CreateDirectory(GameFolder);

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
        public HashSet<string> Profiles = new HashSet<string>();

        public string AddMod(string name = null)
        {
            string mod_name = name ?? RandomName();
            Directory.CreateDirectory(Path.Combine(MO2Folder, "mods", mod_name));
            Mods.Add(mod_name);
            return mod_name;
        }

        public List<string> Mods = new List<string>();

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
            byte[] bytes = new byte[0];
            if (random_fill != 0)
            {
                bytes = new byte[random_fill];
                RNG.NextBytes(bytes);
            }

            var full_path = Path.Combine(ModsFolder, mod_name, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full_path));
            File.WriteAllBytes(full_path, bytes);
            return full_path;
        }

        public void Dispose()
        {
            var exts = new [] {".md", ".exe"};
            Directory.Delete(Path.Combine(WorkingDirectory, ID), true);
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
        private char[] _nameChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ'+=-_ ".ToCharArray();
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

            File.WriteAllLines(Path.Combine(DownloadsFolder, name+".meta"),
                
                new string[]
                {
                    "[General]",
                    "manualURL=<TESTING>"
                });

            return name;
        }

        public void VerifyInstalledFile(string mod, string file)
        {
            var src = Path.Combine(MO2Folder, "mods", mod, file);
            Assert.IsTrue(File.Exists(src), src);

            var dest = Path.Combine(InstallFolder, "mods", mod, file);
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
    }
}
