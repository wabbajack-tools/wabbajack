using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.VirtualFileSystem.Test
{
    public class FileExtractorTests : IAsyncLifetime
    {
        private ITestOutputHelper _helper;
        private IDisposable _unsub;
        private WorkQueue _queue;

        public FileExtractorTests(ITestOutputHelper helper)
        {
            _helper = helper;
            _unsub = Utils.LogMessages.Subscribe(f =>
            {
                try
                {
                    _helper.WriteLine(f.ShortDescription);
                }
                catch (Exception)
                {
                    // ignored
                }
            });
            _queue = new WorkQueue();
        }

        public async Task InitializeAsync()
        {
        }

        public async Task DisposeAsync()
        {
            _unsub.Dispose();
        }
        
        [Fact]
        public async Task CanGatherDataFromZipFiles()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            for (int i = 0; i < 10; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 1024));
            }

            await ZipUpFolder(temp.Dir, archive.Path, false);
            
            var results = await FileExtractor2.GatheringExtract(_queue, new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
                {
                    await using var s = await sfn.GetStream();
                    return await s.xxHashAsync();
                });
            
            Assert.Equal(10, results.Count);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }
        }
        
        [Fact]
        public async Task SmallFilesShouldntCrash()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            for (int i = 0; i < 1; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 10));
            }

            await ZipUpFolder(temp.Dir, archive.Path, false);

            var results = await FileExtractor2.GatheringExtract(_queue, new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
                {
                    await using var s = await sfn.GetStream();
                    return await s.xxHashAsync();
                });
            
            Assert.Single(results);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }
        }

        /* Takes to long to run in CI, enable when needed for verification

        [Fact]
        public async Task MissingFileFromArchiveShouldBeFound()
        {
            FileExtractor2.FavorPerfOverRAM = true;

            // From a bug in 2.3.0.1
            var src = await DownloadMod(Game.SkyrimSpecialEdition, 21166, 136259);
                        
            var results = await FileExtractor2.GatheringExtract(new NativeFileStreamFactory(src), 
                f => true,
                async (p, s) =>
                {
                    await using var stream = await s.GetStream();
                    return stream.Length;
                });
            
            Assert.NotEmpty(results);
        }
*/

        [Fact]
        public async Task CanExtractEmptyFiles()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            
            for (int i = 0; i < 1; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 1024));
            }
            await (await temp.Dir.Combine("empty.txt").Create()).DisposeAsync();

            await ZipUpFolder(temp.Dir, archive.Path, false);
            
            var results = await FileExtractor2.GatheringExtract(_queue, new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
                {
                    await using var s = await sfn.GetStream();
                    return await s.xxHashAsync();
                });
            
            Assert.Equal(2, results.Count);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }
        }
        
        private static Extension OMODExtension = new Extension(".omod");
        private static Extension CRCExtension = new Extension(".crc");

        [Fact]
        public async Task CanGatherDataFromOMODFiles()
        {
            var src = await DownloadMod(Game.Oblivion, 18498);

            await FileExtractor2.GatheringExtract(_queue, new NativeFileStreamFactory(src),
                p => p.Extension == OMODExtension, async (path, sfn) =>
                {
                    await FileExtractor2.GatheringExtract(_queue, sfn, _ => true, async (ipath, isfn) => {
                        // We shouldn't have any .crc files because this file should be recognized as a OMOD and extracted correctly
                        Assert.NotEqual(CRCExtension, ipath.Extension);
                        return 0;
                    });
                    return 0;
                });
        }

        [Fact]
        public async Task CanExtractFOMODFiles()
        {
            var tmpFolder = await TempFolder.Create();
            var src = await DownloadMod(Game.FalloutNewVegas, 52510);
            var newName = src.FileName.RelativeTo(tmpFolder.Dir);
            await src.CopyToAsync(newName);
            
            var ctx = new Context(_queue);
            await ctx.AddRoot(tmpFolder.Dir);

            Assert.NotEmpty(ctx.Index.ByName.Where(f => f.Key.FileName == (RelativePath)"Alternative Repairing.esp"));
        }
        

        [Fact]
        public async Task SmallZipNoLongerCrashes()
        {
            var src = await DownloadMod(Game.Fallout4, 29596, 120918);
            await using var tmpFolder = await TempFolder.Create();
            await FileExtractor2.ExtractAll(_queue, src, tmpFolder.Dir);
        }




        private static readonly Random _rng = new Random();
        private static async Task WriteRandomData(AbsolutePath path, int size)
        {
            var buff = new byte[size];
            _rng.NextBytes(buff);
            await path.WriteAllBytesAsync(buff);
        }
        
        
        private async Task WriteRandomData(Stream path, long size)
        {
            var buff = new byte[size];
            _rng.NextBytes(buff);
            await path.WriteAsync(buff);
        }
        
        private static async Task AddFile(AbsolutePath filename, string text)
        {
            filename.Parent.CreateDirectory();
            await filename.WriteAllTextAsync(text);
        }

        private static async Task ZipUpFolder(AbsolutePath folder, AbsolutePath output, bool deleteSource = true)
        {
            ZipFile.CreateFromDirectory((string)folder, (string)output);
            if (deleteSource) 
                await folder.DeleteDirectory();
        }
        
        
        public static AbsolutePath StagingFolder = ((RelativePath)"NexusDownloads").RelativeToEntryPoint();

        private static async Task<AbsolutePath> DownloadMod(Game game, int mod)
        {
            var client = DownloadDispatcher.GetInstance<NexusDownloader>();
            await client.Prepare();

            var results = await client.Client!.GetModFiles(game, mod);
            var file = results.files.FirstOrDefault(f => f.is_primary) ??
                       results.files.OrderByDescending(f => f.uploaded_timestamp).First();
            return await DownloadNexusFile(game, mod, file);
        }

        public static async Task<AbsolutePath> DownloadNexusFile(Game game, int mod, NexusFileInfo file)
        {
            var src = StagingFolder.Combine(file.file_name);

            if (src.Exists) return src;

            var state = new NexusDownloader.State
            {
                ModID = mod,
                Game = game,
                FileID = file.file_id
            };
            await state.Download(src);
            return src;
        }

        public static async Task<AbsolutePath> DownloadMod(Game game, int mod, int fileId)
        {
            var client = DownloadDispatcher.GetInstance<NexusDownloader>();
            await client.Prepare();

            var results = await client.Client!.GetModFiles(game, mod);
            var file = results.files.FirstOrDefault(f => f.file_id == fileId);
            return await DownloadNexusFile(game, mod, file);

        }
        
    }
}
