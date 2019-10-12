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

            var downloader = new MegaDownloader();
            downloader.Init();
            var state = (AbstractDownloadState)downloader.GetDownloaderState(ini.LoadIniString());

            Assert.IsNotNull(state);

            var converted = state.ViaJSON();
            Assert.IsTrue(converted.Verify());
            var filename = Guid.NewGuid().ToString();

            Assert.IsTrue(converted.IsWhitelisted(new ServerWhitelist {AllowedPrefixes = new List<string>{"https://mega.nz/#!CsMSFaaJ!-uziC4mbJPRy2e4pPk8Gjb3oDT_38Be9fzZ6Ld4NL-k" } }));
            Assert.IsFalse(converted.IsWhitelisted(new ServerWhitelist { AllowedPrefixes = new List<string>{ "blerg" }}));

            converted.Download(new Archive() {Name = "MEGA Test.txt"}, filename);

            Assert.AreEqual("Lb1iTsz3iyZeHGs3e94TVmOhf22sqtHLhqkCdXbjiyc=", Utils.FileSHA256(filename));

            Assert.AreEqual(File.ReadAllText(filename), "Cheese for Everyone!");
        }
    }
}
