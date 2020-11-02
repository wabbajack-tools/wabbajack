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

        public AbsolutePath SourcePath => WorkingDirectory.Combine(ID, "source_folder");
        public AbsolutePath ModsPath => SourcePath.Combine(Consts.MO2ModFolderName);
        public AbsolutePath DownloadsPath => SourcePath.Combine("downloads");

        public AbsolutePath InstallPath => TestFolder.Combine("installed");

        public HashSet<string> Profiles = new HashSet<string>();

        public List<string> Mods = new List<string>();

        public async Task Configure(IEnumerable<(string ModName, bool IsEnabled)> enabledMods = null)
        {
            await SourcePath.Combine("ModOrganizer.ini").WriteAllLinesAsync(
                "[General]", 
                $"gameName={Game.MetaData().MO2Name}", 
                $"gamePath={((string)GameFolder).Replace("\\", "\\\\")}", 
                $"download_directory={DownloadsPath}");

            DownloadsPath.CreateDirectory();
            GameFolder.Combine("Data").CreateDirectory();

            if (enabledMods == null)
            {
                Profiles.Do(profile =>
                {
                    SourcePath.Combine("profiles", profile, "modlist.txt").WriteAllLinesAsync(
                        Mods.Select(s => $"+{s}").ToArray());
                });
            }
            else
            {
                Profiles.Do(profile =>
                {
                    SourcePath.Combine("profiles", profile, "modlist.txt").WriteAllLinesAsync(
                        enabledMods.Select(s => $"{(s.IsEnabled ? "+" : "-")}{s.ModName}").ToArray());
                });
            }
        }

        public string AddProfile(string name = null)
        {
            string profile_name = name ?? RandomName();
            SourcePath.Combine("profiles", profile_name).CreateDirectory();
            Profiles.Add(profile_name);
            return profile_name;
        }

        public async Task<string> AddMod(string name = null)
        {
            string mod_name = name ?? RandomName();
            var mod_folder = SourcePath.Combine(Consts.MO2ModFolderName, (RelativePath)mod_name);
            mod_folder.CreateDirectory();
            await mod_folder.Combine("meta.ini").WriteAllTextAsync("[General]");
            Mods.Add(mod_name);
            return mod_name;
        }

        /// <summary>
        /// Adds a file to the given mod with a given path in the mod. Fills it with random data unless
        /// random_fill == 0;
        /// </summary>
        /// <param name="mod_name"></param>
        /// <param name="path"></param>
        /// <param name="random_fill"></param>
        /// <returns></returns>
        public async Task<AbsolutePath> AddModFile(string mod_name, string path, int random_fill=128)
        {
            var full_path = ModsPath.Combine(mod_name, path);
            full_path.Parent.CreateDirectory();
            await GenerateRandomFileData(full_path, random_fill);
            return full_path;
        }

        public async Task GenerateRandomFileData(AbsolutePath full_path, int random_fill)
        {
            byte[] bytes = new byte[0];
            if (random_fill != 0)
            {
                bytes = new byte[random_fill];
                RNG.NextBytes(bytes);
            }
            await full_path.WriteAllBytesAsync(bytes);
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

        public async ValueTask<string> AddManualDownload(Dictionary<string, byte[]> contents)
        {
            var name = RandomName() + ".zip";

            await using FileStream fs = await DownloadsPath.Combine(name).Create();
            using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create);
            foreach (var (key, value) in contents)
            {
                Utils.Log($"Adding {value.Length.ToFileSizeString()} entry {key}");
                var entry = archive.CreateEntry(key);
                await using var os = entry.Open();
                await os.WriteAsync(value, 0, value.Length);
            }

            await DownloadsPath.Combine(name + Consts.MetaFileExtension).WriteAllLinesAsync(
                "[General]",
                "manualURL=<TESTING>"
            );

            return name;
        }
        
        public async Task VerifyInstalledFile(string mod, string file)
        {
            var src = SourcePath.Combine((string)Consts.MO2ModFolderName, mod, file);
            Assert.True(src.Exists);

            var dest = InstallPath.Combine((string)Consts.MO2ModFolderName, mod, file);
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

            var dest = InstallPath.Combine((string)Consts.GameFolderFilesDir, file);
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
            return InstallPath.Combine((string)Consts.MO2ModFolderName, mod, file);
        }

        public async ValueTask VerifyAllFiles(bool gameFileShouldNotExistInGameFolder = true)
        {
            if (gameFileShouldNotExistInGameFolder)
            {
                foreach (var file in Game.MetaData().RequiredFiles!)
                {
                    Assert.False(InstallPath.Combine(Consts.GameFolderFilesDir, (RelativePath)file).Exists);
                }
            }


            var skipFiles = new []{"portable.txt"}.Select(e => (RelativePath)e).ToHashSet();
            foreach (var destFile in InstallPath.EnumerateFiles())
            {
                var relFile = destFile.RelativeTo(InstallPath);
                if (destFile.InFolder(Consts.LOOTFolderFilesDir.RelativeTo(SourcePath)) || destFile.InFolder(Consts.GameFolderFilesDir.RelativeTo(SourcePath)))
                    continue;
                
                if (!skipFiles.Contains(relFile)) 
                    Assert.True(SourcePath.Combine(relFile).Exists, $"Only in Destination: {relFile}");
            }

            var skipExtensions = new []{".txt", ".ini"}.Select(e => new Extension(e)).ToHashSet();

            foreach (var srcFile in SourcePath.EnumerateFiles())
            {
                var relFile = srcFile.RelativeTo(SourcePath);

                if (relFile.StartsWith("downloads\\"))
                    continue;

                var destFile = InstallPath.Combine(relFile);
                Assert.True(destFile.Exists, $"Only in Source: {relFile}");

                if (!skipExtensions.Contains(srcFile.Extension))
                {
                    Assert.Equal(srcFile.Size, destFile.Size);
                    Assert.Equal(await srcFile.FileHashAsync(), await destFile.FileHashAsync());
                }
            }
        }

        public async ValueTask<AbsolutePath> AddGameFile(string path, int i)
        {
            var fullPath = GameFolder.Combine(path);
            fullPath.Parent.CreateDirectory();
            await GenerateRandomFileData(fullPath, i);
            return fullPath;
        }

        public void CreatePaths()
        {
            SourcePath.CreateDirectory();
            DownloadsPath.CreateDirectory();
            InstallPath.CreateDirectory();
        }
    }
}
