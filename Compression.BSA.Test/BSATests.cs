using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Compression.BSA.Test
{
    [TestClass]
    public class BSATests
    {
        private static string _stagingFolder = "NexusDownloads";
        private static string _bsaFolder = "BSAs";
        private static string _testDir = "BSA Test Dir";
        private static string _tempDir = "BSA Temp Dir";

        public TestContext TestContext { get; set; }

        private static WorkQueue Queue { get; set; }

        [ClassInitialize]
        public static async Task Setup(TestContext testContext)
        {
            Queue = new WorkQueue();
            Utils.LogMessages.Subscribe(f => testContext.WriteLine(f.ShortDescription));
            if (!Directory.Exists(_stagingFolder))
                Directory.CreateDirectory(_stagingFolder);
            
            if (!Directory.Exists(_bsaFolder))
                Directory.CreateDirectory(_bsaFolder);

            var modIDs = new[]
            {
                (Game.SkyrimSpecialEdition, 12604), // SkyUI
                (Game.Skyrim, 3863), // SkyUI
                (Game.Skyrim, 51473), // iNeed
                (Game.Fallout4, 22223) // 10mm SMG
            };

            await Task.WhenAll(modIDs.Select(async (info) =>
            {
                var filename = await DownloadMod(info);
                var folder = Path.Combine(_bsaFolder, info.Item1.ToString(), info.Item2.ToString());
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                await FileExtractor.ExtractAll(Queue, filename, folder);
            }));
        }

        private static async Task<string> DownloadMod((Game, int) info)
        {
            using (var client = await NexusApiClient.Get())
            {
                var results = await client.GetModFiles(info.Item1, info.Item2);
                var file = results.files.FirstOrDefault(f => f.is_primary) ??
                           results.files.OrderByDescending(f => f.uploaded_timestamp).First();
                var src = Path.Combine(_stagingFolder, file.file_name);

                if (File.Exists(src)) return src;

                var state = new NexusDownloader.State
                {
                    ModID = info.Item2.ToString(),
                    GameName = GameRegistry.Games[info.Item1].NexusName,
                    FileID = file.file_id.ToString()
                };
                await state.Download(src);
                return src;
            }
        }

        public static IEnumerable<object[]> BSAs()
        {
            return Directory.EnumerateFiles(_bsaFolder, "*", DirectoryEnumerationOptions.Recursive)
                .Where(f => Consts.SupportedBSAs.Contains(Path.GetExtension(f)))
                .Select(nm => new object[] {nm});
        }

        [TestMethod]
        [DataTestMethod]
        [DynamicData(nameof(BSAs), DynamicDataSourceType.Method)]
        public async Task BSACompressionRecompression(string bsa)
        {
            TestContext.WriteLine($"From {bsa}");
            TestContext.WriteLine("Cleaning Output Dir");
            if (Directory.Exists(_tempDir)) Utils.DeleteDirectory(_tempDir);
            Directory.CreateDirectory(_tempDir);

            TestContext.WriteLine($"Reading {bsa}");
            string tempFile = Path.Combine("tmp.bsa");
            using (var a = BSADispatch.OpenRead(bsa))
            {
                await a.Files.PMap(Queue, file =>
                {
                    var absName = Path.Combine(_tempDir, file.Path);
                    ViaJson(file.State);

                    if (!Directory.Exists(Path.GetDirectoryName(absName)))
                        Directory.CreateDirectory(Path.GetDirectoryName(absName));


                    using (var fs = File.OpenWrite(absName))
                    {
                        file.CopyDataTo(fs);
                    }

                    Assert.AreEqual(file.Size, new FileInfo(absName).Length);
                });

                Console.WriteLine($"Building {bsa}");

                using (var w = ViaJson(a.State).MakeBuilder())
                {
                    await a.Files.PMap(Queue, file =>
                    {
                        var absPath = Path.Combine(_tempDir, file.Path);
                        using (var str = File.OpenRead(absPath))
                        {
                            w.AddFile(ViaJson(file.State), str);
                        }
                    });
                    w.Build(tempFile);
                }

                Console.WriteLine($"Verifying {bsa}");
                using (var b = BSADispatch.OpenRead(tempFile))
                {

                    Console.WriteLine($"Performing A/B tests on {bsa}");
                    Assert.AreEqual(JsonConvert.SerializeObject(a.State), JsonConvert.SerializeObject(b.State));

                    // Check same number of files
                    Assert.AreEqual(a.Files.Count(), b.Files.Count());
                    var idx = 0;

                    await a.Files.Zip(b.Files, (ai, bi) => (ai, bi))
                                .PMap(Queue, pair =>
                                {
                                    idx++;
                                    Assert.AreEqual(JsonConvert.SerializeObject(pair.ai.State),
                                        JsonConvert.SerializeObject(pair.bi.State));
                                    //Console.WriteLine($"   - {pair.ai.Path}");
                                    Assert.AreEqual(pair.ai.Path, pair.bi.Path);
                                    //Equal(pair.ai.Compressed, pair.bi.Compressed);
                                    Assert.AreEqual(pair.ai.Size, pair.bi.Size);
                                    CollectionAssert.AreEqual(GetData(pair.ai), GetData(pair.bi));
                                });
                }
            }
        }

        private static byte[] GetData(IFile pairAi)
        {
            using (var ms = new MemoryStream())
            {
                pairAi.CopyDataTo(ms);
                return ms.ToArray();
            }
        }

        public static T ViaJson<T>(T i)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(i, settings), settings);
        }
    }
}
