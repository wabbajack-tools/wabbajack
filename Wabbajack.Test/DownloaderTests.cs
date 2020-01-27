using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Alphaleonis.Win32.Filesystem;
using CefSharp;
using CefSharp.OffScreen;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Common.StatusFeed;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.LibCefHelpers;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;
using File = Alphaleonis.Win32.Filesystem.File;
using Game = Wabbajack.Common.Game;

namespace Wabbajack.Test
{
    [TestClass]
    public class DownloaderTests
    {
        static DownloaderTests()
        {
            Helpers.Init();
        }

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task Setup()
        {
            Helpers.Init();
            Utils.LogMessages.OfType<IInfo>().Subscribe(onNext: msg => TestContext.WriteLine(msg.ShortDescription));
            Utils.LogMessages.OfType<IUserIntervention>().Subscribe(msg =>
                TestContext.WriteLine("ERROR: User intervention required: " + msg.ShortDescription));

        }

        [TestMethod]
        public void TestAllPrepares()
        {
            DownloadDispatcher.Downloaders.Do(d => d.Prepare());
        }

        [TestMethod]
        public async Task MegaDownload()
        {
            var ini = @"[General]
                        directURL=https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k");

            Assert.AreEqual("https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k",
                ((MegaDownloader.State)url_state).Url);


            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>{"https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>{ "blerg" }}));

            await converted.Download(new Archive {Name = "MEGA Test.txt"}, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public async Task DropboxTests()
        {
            var ini = @"[General]
                        directURL=https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0");

            Assert.AreEqual("https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=1", 
                ((HTTPDownloader.State)url_state).Url);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "blerg" } }));

            await converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public async Task GoogleDriveTests()
        {
            var ini = @"[General]
                        directURL=https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing");

            Assert.AreEqual("1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_",
                ((GoogleDriveDownloader.State)url_state).Id);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string> { "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string>()}));

            await converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public async Task HttpDownload()
        {
            var ini = @"[General]
                        directURL=http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt");

            Assert.AreEqual("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt",
                ((HTTPDownloader.State)url_state).Url);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "http://build.wabbajack.org/" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public async Task ManualDownload()
        {
            var ini = @"[General]
                        manualURL=http://build.wabbajack.org/WABBAJACK_TEST_FILE.zip";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "http://build.wabbajack.org/" } }));

            
            // Doesn't work well on the test server, so we're disabling for now
            //await converted.Download(new Archive { Name = "WABBAJACK_TEST_FILE.zip", Size = 20, Hash = "eSIyd+KOG3s="}, filename);
            //Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));
            //Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        /*
        [TestMethod]
        public async Task MediaFireDownload()
        {
            var ini = @"[General]
                    directURL=http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt");

            Assert.AreEqual("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt",
                ((MediaFireDownloader.State) url_state).Url);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist
                {AllowedPrefixes = new List<string> {"http://www.mediafire.com/file/agiqzm1xwebczpx/"}}));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>()}));

            await converted.Download(new Archive {Name = "Media Fire Test.txt"}, filename);

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");

        }*/

        [TestMethod]
        public async Task NexusDownload()
        {
            var old_val = NexusApiClient.CacheMethod;
            try
            {
                NexusApiClient.CacheMethod = null;
                var ini = @"[General]
                        gameName=SkyrimSE
                        modID = 12604
                        fileID=35407";

                var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

                Assert.IsNotNull(state);


                var converted = await state.RoundTripState();
                Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
                // Exercise the cache code
                Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
                var filename = Guid.NewGuid().ToString();

                Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> () }));

                await converted.Download(new Archive { Name = "SkyUI.7z" }, filename);

                Assert.AreEqual(filename.FileHash(), "dF2yafV2Oks=");

            }
            finally
            {
                NexusApiClient.CacheMethod = old_val;
            }
        }

        [TestMethod]
        public async Task ModDbTests()
        {
            var ini = @"[General]
                        directURL=https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause");

            Assert.AreEqual("https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause",
                ((ModDBDownloader.State)url_state).Url);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "moddbtest.7z" }, filename);

            Assert.AreEqual("2lZt+1h6wxM=", filename.FileHash());
        }

        [TestMethod]
        public async Task LoversLabDownload()
        {
            await DownloadDispatcher.GetInstance<LoversLabDownloader>().Prepare();
            var ini = @"[General]
                        directURL=https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r=737123&confirm=1&t=1";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            /*var url_state = DownloadDispatcher.ResolveArchive("https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r=737123&confirm=1&t=1");
            Assert.AreEqual("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt",
                ((HTTPDownloader.State)url_state).Url);
                */
            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "LoversLab Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }
        
        [TestMethod]
        public async Task VectorPlexusDownload()
        {
            await DownloadDispatcher.GetInstance<VectorPlexusDownloader>().Prepare();
            var ini = @"[General]
                        directURL=https://vectorplexus.com/files/file/290-wabbajack-test-file";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            /*var url_state = DownloadDispatcher.ResolveArchive("https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r=737123&confirm=1&t=1");
            Assert.AreEqual("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt",
                ((HTTPDownloader.State)url_state).Url);
                */
            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "Vector Plexus Test.zip" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        /* WAITING FOR APPROVAL BY MODERATOR (you need to have your submitted file approved before it goes live)
         [TestMethod]
        public async Task DeadlyStreamDownloader()
        {
            await DownloadDispatcher.GetInstance<DeadlyStreamDownloader>().Prepare();
            const string ini = "[General]\n" +
                               "directURL=https://deadlystream.com/files/file/1550-wabbajack-test-file/";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "DeadlyStream Test.zip" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", filename.FileHash());

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }*/

        [TestMethod]
        public async Task GameFileSourceDownload()
        {
            // Test mode off for this test
            Consts.TestMode = false;
            await DownloadDispatcher.GetInstance<LoversLabDownloader>().Prepare();
            var ini = $@"[General]
                        gameName={Game.SkyrimSpecialEdition.MetaData().MO2ArchiveName}
                        gameFile=Data/Update.esm";

            var state = (AbstractDownloadState)await DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = await state.RoundTripState();
            Assert.IsTrue(await converted.Verify(new Archive{Size = 20}));
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            await converted.Download(new Archive { Name = "Update.esm" }, filename);

            Assert.AreEqual("/DLG/LjdGXI=", Utils.FileHash(filename));
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(Game.SkyrimSpecialEdition.MetaData().GameLocation(), "Data/Update.esm")), File.ReadAllBytes(filename));
            Consts.TestMode = true;
        }

    }



}
