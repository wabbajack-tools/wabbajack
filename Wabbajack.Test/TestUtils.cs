using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Xunit;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Test
{
    public class TestUtils : IAsyncDisposable
    {
        private static Random _rng = new Random();
        public TestUtils()
        {
            ID = RandomName();
            WorkingDirectory = ((RelativePath)"tmp_data").RelativeToEntryPoint();
        }

        public AbsolutePath WorkingDirectory { get;}
        public string ID { get; }
        public Random RNG => _rng;

        public Game Game { get; set; }

        public AbsolutePath TestFolder => WorkingDirectory.Combine(ID);
        public AbsolutePath GameFolder => WorkingDirectory.Combine(ID, "game_folder");

        public AbsolutePath MO2Folder => WorkingDirectory.Combine(ID, "mo2_folder");
        public AbsolutePath ModsFolder => MO2Folder.Combine(Consts.MO2ModFolderName);
        public AbsolutePath DownloadsFolder => MO2Folder.Combine("downloads");

        public AbsolutePath InstallFolder => TestFolder.Combine("installed");

        public HashSet<string> Profiles = new HashSet<string>();

        public List<string> Mods = new List<string>();

        public async Task Configure(IEnumerable<(string ModName, bool IsEnabled)> enabledMods = null)
        {
            await MO2Folder.Combine("ModOrganizer.ini").WriteAllLinesAsync(
                "[General]", 
                $"gameName={Game.MetaData().MO2Name}", 
                $"gamePath={((string)GameFolder).Replace("\\", "\\\\")}", 
                $"download_directory={DownloadsFolder}");

            DownloadsFolder.CreateDirectory();
            GameFolder.Combine("Data").CreateDirectory();

            if (enabledMods == null)
            {
                Profiles.Do(profile =>
                {
                    MO2Folder.Combine("profiles", profile, "modlist.txt").WriteAllLines(
                        Mods.Select(s => $"+{s}").ToArray());
                });
            }
            else
            {
                Profiles.Do(profile =>
                {
                    MO2Folder.Combine("profiles", profile, "modlist.txt").WriteAllLines(
                        enabledMods.Select(s => $"{(s.IsEnabled ? "+" : "-")}{s.ModName}").ToArray());
                });
            }
        }

        public string AddProfile(string name = null)
        {
            string profile_name = name ?? RandomName();
            MO2Folder.Combine("profiles", profile_name).CreateDirectory();
            Profiles.Add(profile_name);
            return profile_name;
        }

        public string AddMod(string name = null)
        {
            lock (this)
            {
                string mod_name = name ?? RandomName();
                var mod_folder = MO2Folder.Combine(Consts.MO2ModFolderName, (RelativePath)mod_name);
                mod_folder.CreateDirectory();
                mod_folder.Combine("meta.ini").WriteAllText("[General]");
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
        public AbsolutePath AddModFile(string mod_name, string path, int random_fill=128)
        {


            var full_path = ModsFolder.Combine(mod_name, path);
            full_path.Parent.CreateDirectory();
            GenerateRandomFileData(full_path, random_fill);
            return full_path;
        }

        public void GenerateRandomFileData(AbsolutePath full_path, int random_fill)
        {
            byte[] bytes = new byte[0];
            if (random_fill != 0)
            {
                bytes = new byte[random_fill];
                RNG.NextBytes(bytes);
            }
            full_path.WriteAllBytes(bytes);
        }

        public static byte[] RandomData(int? size = null, int maxSize = 1024)
        {
            if (size == null)
                size = _rng.Next(1, maxSize);
            var arr = new byte[(int) size];
            _rng.NextBytes(arr);
            return arr;
        }

        public async ValueTask DisposeAsync()
        {
            var exts = new[] { ".md", ".exe" };
            await WorkingDirectory.Combine(ID).DeleteDirectory();
            Profiles.Do(p =>
            {
                foreach (var ext in exts)
                {
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

        public byte[] RandomData(int size = 0)
        {
            if (size == 0)
                size = _rng.Next(256);
            var data = new byte[size];
            _rng.NextBytes(data);
            return data;
        }

        public string AddManualDownload(Dictionary<string, byte[]> contents)
        {
            var name = RandomName() + ".zip";

            using FileStream fs = DownloadsFolder.Combine(name).Create();
            using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create);
            contents.Do(kv =>
            {
                var entry = archive.CreateEntry(kv.Key);
                using (var os = entry.Open())
                    os.Write(kv.Value, 0, kv.Value.Length);
            });

            DownloadsFolder.Combine(name + Consts.MetaFileExtension).WriteAllLines(
                    "[General]",
                    "manualURL=<TESTING>"
                );

            return name;
        }

        public async Task VerifyInstalledFile(string mod, string file)
        {
            var src = MO2Folder.Combine((string)Consts.MO2ModFolderName, mod, file);
            Assert.True(src.Exists);

            var dest = InstallFolder.Combine((string)Consts.MO2ModFolderName, mod, file);
            Assert.True(dest.Exists, $"Destination {dest} doesn't exist");

            var srcData = await src.ReadAllBytesAsync();
            var destData = await dest.ReadAllBytesAsync();

            Assert.Equal(srcData.Length, destData.Length);

            for(int x = 0; x < srcData.Length; x++)
            {
                if (srcData[x] != destData[x])
                    Assert.True(false, $"Index {x} of {mod}\\{file} are not the same");
            }
        }
        
        public async Task VerifyInstalledGameFile(string file)
        {
            var src = GameFolder.Combine(file);
            Assert.True(src.Exists);

            var dest = InstallFolder.Combine((string)Consts.GameFolderFilesDir, file);
            Assert.True(dest.Exists);

            var srcData = await src.ReadAllBytesAsync();
            var destData = await dest.ReadAllBytesAsync();

            Assert.Equal(srcData.Length, destData.Length);

            for(int x = 0; x < srcData.Length; x++)
            {
                if (srcData[x] != destData[x])
                    Assert.True(false, $"Index {x} of {Consts.GameFolderFilesDir}\\{file} are not the same");
            }
        }
        public AbsolutePath PathOfInstalledFile(string mod, string file)
        {
            return InstallFolder.Combine((string)Consts.MO2ModFolderName, mod, file);
        }

        public void VerifyAllFiles(bool gameFileShouldNotExistInGameFolder = true)
        {
            if (gameFileShouldNotExistInGameFolder)
            {
                foreach (var file in Game.MetaData().RequiredFiles!)
                {
                    Assert.False(InstallFolder.Combine(Consts.GameFolderFilesDir, (RelativePath)file).Exists);
                }
            }


            var skipFiles = new []{"portable.txt"}.Select(e => (RelativePath)e).ToHashSet();
            foreach (var destFile in InstallFolder.EnumerateFiles())
            {
                var relFile = destFile.RelativeTo(InstallFolder);
                if (destFile.InFolder(Consts.LOOTFolderFilesDir.RelativeTo(MO2Folder)) || destFile.InFolder(Consts.GameFolderFilesDir.RelativeTo(MO2Folder)))
                    continue;
                
                if (!skipFiles.Contains(relFile)) 
                    Assert.True(MO2Folder.Combine(relFile).Exists, $"Only in Destination: {relFile}");
            }

            var skipExtensions = new []{".txt", ".ini"}.Select(e => new Extension(e)).ToHashSet();

            foreach (var srcFile in MO2Folder.EnumerateFiles())
            {
                var relFile = srcFile.RelativeTo(MO2Folder);

                if (relFile.StartsWith("downloads\\"))
                    continue;

                var destFile = InstallFolder.Combine(relFile);
                Assert.True(destFile.Exists, $"Only in Source: {relFile}");

                if (!skipExtensions.Contains(srcFile.Extension))
                {
                    Assert.Equal(srcFile.Size, destFile.Size);
                    Assert.Equal(srcFile.FileHash(), destFile.FileHash());
                }
            }
        }

        public AbsolutePath AddGameFile(string path, int i)
        {
            var fullPath = GameFolder.Combine(path);
            fullPath.Parent.CreateDirectory();
            GenerateRandomFileData(fullPath, i);
            return fullPath;
        }
    }
}
