using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Test.ListValidation
{
    [TestClass]
    public class ListValidation
    {
        [TestInitialize]
        public void Setup()
        {
            Directory.CreateDirectory(Consts.ModListDownloadFolder);
            Utils.SetLoggerFn(s => TestContext.WriteLine(s));
            WorkQueue.Init();
            Utils.ToJSON("ff");

        }

        private TestContext testContextInstance;
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetModLists), DynamicDataSourceType.Method)]
        public void ValidateModLists(ModlistMetadata list)
        {
            Log($"Testing {list.Links.MachineURL} - {list.Title}");

            var state = DownloadDispatcher.ResolveArchive(list.Links.Download);

            Log($"Downloading {list.Links.MachineURL} - {list.Title}");
            
            var modlist_path = Path.Combine(Consts.ModListDownloadFolder, list.Links.MachineURL + ".wabbajack");
            state.Download(modlist_path);

            Log($"Loading {modlist_path}");

            var installer = Installer.LoadFromFile(modlist_path);

            Log($"{installer.Archives.Count} archives to validate");

            var invalids = installer.Archives
                .PMap(archive =>
                {
                    Log($"Validating: {archive.Name}");
                    return new {archive, is_valid = archive.State.Verify()};
                })
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

        public static IEnumerable<object[]> GetModLists()
        {
            return ModlistMetadata.LoadFromGithub().Select(l => new object[] {l});
        }
    }
}
