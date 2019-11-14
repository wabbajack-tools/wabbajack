using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
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
        private static string StagingFolder = "NexusDownloads";
        private static string BSAFolder = "BSAs";
        private static string TestDir = "BSA Test Dir";
        private static string TempDir = "BSA Temp Dir";

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static async Task Setup(TestContext TestContext)
        {

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f));
            if (!Directory.Exists(StagingFolder))
                Directory.CreateDirectory(StagingFolder);
            
            if (!Directory.Exists(BSAFolder))
                Directory.CreateDirectory(BSAFolder);

            var mod_ids = new[]
            {
                (Game.SkyrimSpecialEdition, 12604), // SkyUI
                (Game.Skyrim, 3863), // SkyUI
                (Game.Skyrim, 51473), // iNeed
                (Game.Fallout4, 22223) // 10mm SMG
            };

            foreach (var info in mod_ids)
            {
                var filename = DownloadMod(info);
                var folder = Path.Combine(BSAFolder, info.Item1.ToString(), info.Item2.ToString());
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                await FileExtractor.ExtractAll(filename, folder);
            }
        }

        private static string DownloadMod((Game, int) info)
        {
            using (var client = new NexusApiClient())
            {
                var results = client.GetModFiles(info.Item1, info.Item2);
                var file = results.FirstOrDefault(f => f.is_primary) ??
                           results.OrderByDescending(f => f.uploaded_timestamp).First();
                var src = Path.Combine(StagingFolder, file.file_name);

                if (File.Exists(src)) return src;

                var state = new NexusDownloader.State
                {
                    ModID = info.Item2.ToString(),
                    GameName = GameRegistry.Games[info.Item1].NexusName,
                    FileID = file.file_id.ToString()
                };
                state.Download(src);
                return src;
            }
        }

        public static IEnumerable<object[]> BSAs()
        {
            return Directory.EnumerateFiles(BSAFolder, "*", DirectoryEnumerationOptions.Recursive)
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
            if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
            //if (Directory.Exists(ArchiveTempDir)) Directory.Delete(ArchiveTempDir, true);
            Directory.CreateDirectory(TempDir);

            TestContext.WriteLine($"Reading {bsa}");
            using (var a = await BSADispatch.OpenRead(bsa))
            {
                await a.Files.UnorderedParallelDo(async file =>
                {
                    var abs_name = Path.Combine(TempDir, file.Path);
                    ViaJson(file.State);

                    if (!Directory.Exists(Path.GetDirectoryName(abs_name)))
                        Directory.CreateDirectory(Path.GetDirectoryName(abs_name));


                    using (var fs = File.OpenWrite(abs_name))
                    {
                        await file.CopyDataToAsync(fs);
                    }

                    Assert.AreEqual(file.Size, new FileInfo(abs_name).Length);
                });

                Console.WriteLine($"Building {bsa}");
                string TempFile = Path.Combine("tmp.bsa");

                using (var w = ViaJson(a.State).MakeBuilder())
                {
                    await a.Files.UnorderedParallelDo(async file =>
                    {
                        var abs_path = Path.Combine(TempDir, file.Path);
                        using (var str = File.OpenRead(abs_path))
                        {
                            await w.AddFile(ViaJson(file.State), str);
                        }
                    });
                    await w.Build(TempFile);
                }

                Console.WriteLine($"Verifying {bsa}");
                using (var b = await BSADispatch.OpenRead(TempFile))
                {

                    Console.WriteLine($"Performing A/B tests on {bsa}");
                    Assert.AreEqual(JsonConvert.SerializeObject(a.State), JsonConvert.SerializeObject(b.State));

                    // Check same number of files
                    Assert.AreEqual(a.Files.Count(), b.Files.Count());
                    var idx = 0;

                    await a.Files.Zip(b.Files, (ai, bi) => (ai, bi))
                                .UnorderedParallelDo(async pair =>
                                {
                                    idx++;
                                    Assert.AreEqual(JsonConvert.SerializeObject(pair.ai.State),
                                        JsonConvert.SerializeObject(pair.bi.State));
                                    //Console.WriteLine($"   - {pair.ai.Path}");
                                    Assert.AreEqual(pair.ai.Path, pair.bi.Path);
                                    //Equal(pair.ai.Compressed, pair.bi.Compressed);
                                    Assert.AreEqual(pair.ai.Size, pair.bi.Size);
                                    CollectionAssert.AreEqual(await GetData(pair.ai), await GetData(pair.bi));
                                });
                }
            }
        }

        private static async Task<byte[]> GetData(IFile pairAi)
        {
            using (var ms = new MemoryStream())
            {
                await pairAi.CopyDataToAsync(ms);
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
