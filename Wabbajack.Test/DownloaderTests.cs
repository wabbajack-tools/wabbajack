using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Test
{
    [TestClass]
    public class DownloaderTests
    {
        [TestMethod]
        public void MegaDownload()
        {
            var ini = @"[General]
                        directURL=https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>{"https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>{ "blerg" }}));

            converted.Download(new Archive {Name = "MEGA Test.txt"}, filename);

            Assert.AreEqual("Lb1iTsz3iyZeHGs3e94TVmOhf22sqtHLhqkCdXbjiyc=", Utils.FileSHA256(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void DropboxTests()
        {
            var ini = @"[General]
                        directURL=https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?dl=0";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "https://www.dropbox.com/s/5hov3m2pboppoc2/WABBAJACK_TEST_FILE.txt?" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "blerg" } }));

            converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("Lb1iTsz3iyZeHGs3e94TVmOhf22sqtHLhqkCdXbjiyc=", Utils.FileSHA256(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void GoogleDriveTests()
        {
            var ini = @"[General]
                        directURL=https://drive.google.com/file/d/1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_/view?usp=sharing";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string> { "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { GoogleIDs = new List<string>()}));

            converted.Download(new Archive { Name = "MEGA Test.txt" }, filename);

            Assert.AreEqual("Lb1iTsz3iyZeHGs3e94TVmOhf22sqtHLhqkCdXbjiyc=", Utils.FileSHA256(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }

        [TestMethod]
        public void HttpDownload()
        {
            var ini = @"[General]
                        directURL=https://raw.githubusercontent.com/wabbajack-tools/opt-out-lists/master/ServerWhitelist.yml";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> { "https://raw.githubusercontent.com/" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            converted.Download(new Archive { Name = "Github Test Test.txt" }, filename);

            Assert.IsTrue(File.ReadAllText(filename).StartsWith("# Server whitelist for Wabbajack"));
        }

        [TestMethod]
        public void NexusDownload()
        {
            var ini = @"[General]
                        gameName=SkyrimSE
                        modID = 12604
                        fileID=35407";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string> () }));

            converted.Download(new Archive { Name = "SkyUI.7z" }, filename);

            Assert.AreEqual(filename.FileSHA256(), "U3Xg6RBR9XrUY9/jQSu6WKu5dfhHmpaN2dTl0ylDFmI=");
        }

        [TestMethod]
        public void ModDbTests()
        {
            var ini = @"[General]
                        directURL=https://www.moddb.com/downloads/start/124908?referer=https%3A%2F%2Fwww.moddb.com%2Fmods%2Fautopause";

            var state = (AbstractDownloadState)DownloadDispatcher.ResolveArchive(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>() }));

            converted.Download(new Archive { Name = "moddbtest.7z" }, filename);

            Assert.AreEqual("lUvpEjqxfyidBONSHcDy6EnZIPpAD2K4rkJ5ejCXc2k=", filename.FileSHA256());
        }
    }



}
