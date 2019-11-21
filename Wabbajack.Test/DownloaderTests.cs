using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Test
{
    [TestClass]
    public class DownloaderTests
    {
        [TestInitialize]
        public void Setup()
        {
        }

        [TestMethod]
        public void TestAllPrepares()
        {
            DownloadDispatcher.Downloaders.Do(d => d.Prepare());
        }

        [TestMethod]
        public void MegaDownload()
        {
            var ini = @"[General]
                        directURL=https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k");

            Assert.AreEqual("https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k",
                ((MegaDownloader.State)url_state).Url);


            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>{"https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>{ "blerg" }}));

            converted.Download(new Archive {Name = "MEGA Test.txt"}, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void DropboxTests()
        {
            var ini = @"[General]
                        directURL=https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0");

            Assert.AreEqual("https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=1", 
                ((HTTPDownloader.State)url_state).Url);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "blerg" } }));

            converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void GoogleDriveTests()
        {
            var ini = @"[General]
                        directURL=https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing");

            Assert.AreEqual("1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_",
                ((GoogleDriveDownloader.State)url_state).Id);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string> { "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string>()}));

            converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void HttpDownload()
        {
            var ini = @"[General]
                        directURL=http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt");

            Assert.AreEqual("http://build.wabbajack.org/WABBAJACK_TEST_FILE.txt",
                ((HTTPDownloader.State)url_state).Url);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "http://build.wabbajack.org/" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void ManualDownload()
        {
            var ini = @"[General]
                        manualURL=http://build.wabbajack.org/WABBAJACK_TEST_FILE.zip";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "http://build.wabbajack.org/" } }));

            converted.Download(new Archive { Name = "WABBAJACK_TEST_FILE.zip", Size = 20, Hash = "eSIyd+KOG3s="}, filename);

            Assert.AreEqual("eSIyd+KOG3s=", Utils.FileHash(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void MediaFireDownload()
        {
            var ini = @"[General]
                    directURL=http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt";

            var state = (AbstractDownloadState) DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt");

            Assert.AreEqual("http://www.mediafire.com/file/agiqzm1xwebczpx/WABBAJACK_TEST_FILE.txt",
                ((MediaFireDownloader.State) url_state).Url);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist
                {AllowedPrefixes = new List<string> {"http://www.mediafire.com/file/agiqzm1xwebczpx/"}}));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>()}));

            converted.Download(new Archive {Name = "Media Fire Test.txt"}, filename);

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");

        }

        [TestMethod]
        public void NexusDownload()
        {
            var old_val = NexusApiClient.UseLocalCache;
            try
            {
                NexusApiClient.UseLocalCache = false;
                var ini = @"[General]
                        gameName=SkyrimSE
                        modID = 12604
                        fileID=35407";

                var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

                Assert.IsNotNull(state);


                var converted = state.ViaJSON();
                Assert.IsTrue(converted.Verify());
                // Exercise the cache code
                Assert.IsTrue(converted.Verify());
                var filename = Guid.NewGuid().ToString();

                Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> () }));

                converted.Download(new Archive { Name = "SkyUI.7z" }, filename);

                Assert.AreEqual(filename.FileHash(), "dF2yafV2Oks=");

            }
            finally
            {
                NexusApiClient.UseLocalCache = old_val;
            }
        }

        [TestMethod]
        public void ModDbTests()
        {
            var ini = @"[General]
                        directURL=https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var url_state = DownloadDispatcher.ResolveArchive(
                "https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause");

            Assert.AreEqual("https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause",
                ((ModDBDownloader.State)url_state).Url);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            converted.Download(new Archive { Name = "moddbtest.7z" }, filename);

            Assert.AreEqual("2lZt+1h6wxM=", filename.FileHash());
        }
    }



}
