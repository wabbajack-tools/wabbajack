using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;

namespace Wabbajack.Test.ListValidation
{
    [TestClass]
    public class ListValidation
    {
        [ClassInitialize]
        public static async Task SetupNexus(TestContext context)
        {
            Utils.LogMessages.Subscribe(m => context.WriteLine(m.ToString()));
            var api = new NexusApiClient();
            await api.ClearUpdatedModsInCache();
        }

        private WorkQueue Queue { get; set; }
        [TestInitialize]
        public void Setup()
        {
            Directory.CreateDirectory(Consts.ModListDownloadFolder);
            Utils.LogMessages.Subscribe(s => TestContext.WriteLine(s.ToString()));
            Queue = new WorkQueue();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Queue.Dispose();
            Queue = null;
        }


        public TestContext TestContext { get; set; }

        [TestCategory("ListValidation")]
        [DataTestMethod]
        [DynamicData(nameof(GetModLists), DynamicDataSourceType.Method)]
        public async Task ValidateModLists(string name, ModlistMetadata list)
        {
            Log($"Testing {list.Links.MachineURL} - {list.Title}");
            var modlist_path = Path.Combine(Consts.ModListDownloadFolder, list.Links.MachineURL + ".wabbajack");

            if (list.NeedsDownload(modlist_path))
            {
                var state = DownloadDispatcher.ResolveArchive(list.Links.Download);
                Log($"Downloading {list.Links.MachineURL} - {list.Title}");
                await state.Download(modlist_path);
            }
            else
            {
                Log($"No changes detected from downloaded modlist");
            }


            Log($"Loading {modlist_path}");

            var installer = AInstaller.LoadFromFile(modlist_path);

            Log($"{installer.Archives.Count} archives to validate");

            var invalids = (await installer.Archives
                .PMap(Queue, async archive =>
                {
                    Log($"Validating: {archive.Name}");
                    return new {archive, is_valid = await archive.State.Verify()};
                }))
                .Where(a => !a.is_valid)
                .ToList();

            DownloadDispatcher.PrepareAll(installer.Archives.Select(a => a.State));

            Log("Invalid Archives");
            foreach (var invalid in invalids)
            {
                Log(invalid.archive.State.GetReportEntry(invalid.archive));
            }

            Assert.AreEqual(invalids.Count, 0, "There were invalid archives");
        }

        void Log(string msg)
        {
            TestContext.WriteLine(msg);
        }

        public static async Task<IEnumerable<object[]>> GetModLists()
        {
            return (await ModlistMetadata.LoadFromGithub()).Select(l => new object[] {l.Title, l});
        }
    }
}
