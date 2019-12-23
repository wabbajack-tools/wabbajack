using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    [TestClass]
    public class MO2Tests
    {
        #region CheckValidInstallPath
        [TestMethod]
        public void CheckValidInstallPath_Empty()
        {
            using (var tempDir = new TempFolder())
            {
                Assert.IsTrue(MO2Installer.CheckValidInstallPath(tempDir.Dir.FullName, downloadFolder: null).Succeeded);
            }
        }

        [TestMethod]
        public void CheckValidInstallPath_DoesNotExist()
        {
            using (var tempDir = new TempFolder())
            {
                Assert.IsTrue(MO2Installer.CheckValidInstallPath(Path.Combine(tempDir.Dir.FullName, "Subfolder"), downloadFolder: null).Succeeded);
            }
        }

        [TestMethod]
        public void CheckValidInstallPath_Invalid()
        {
            using (var tempDir = new TempFolder())
            {
                Assert.IsFalse(MO2Installer.CheckValidInstallPath($"{tempDir.Dir.FullName}/*", downloadFolder: null).Succeeded);
            }
        }

        [TestMethod]
        public void CheckValidInstallPath_HasModlist()
        {
            using (var tempDir = new TempFolder())
            {
                File.Create(Path.Combine(tempDir.Dir.FullName, $"ModOrganizer.exe"));
                File.Create(Path.Combine(tempDir.Dir.FullName, $"modlist{ExtensionManager.Extension}"));
                Assert.IsFalse(MO2Installer.CheckValidInstallPath(tempDir.Dir.FullName, downloadFolder: null).Succeeded);
            }
        }

        [TestMethod]
        public void CheckValidInstallPath_ProperOverwrite()
        {
            using (var tempDir = new TempFolder())
            {
                File.Create(Path.Combine(tempDir.Dir.FullName, $"ModOrganizer.exe"));
                Assert.IsTrue(MO2Installer.CheckValidInstallPath(tempDir.Dir.FullName, downloadFolder: null).Succeeded);
            }
        }

        [TestMethod]
        public void CheckValidInstallPath_ImproperOverwrite()
        {
            using (var tempDir = new TempFolder())
            {
                File.Create(Path.Combine(tempDir.Dir.FullName, $"someFile.txt"));
                Assert.IsFalse(MO2Installer.CheckValidInstallPath(tempDir.Dir.FullName, downloadFolder: null).Succeeded);
            }
        }

        [TestMethod]
        public void CheckValidInstallPath_OverwriteFilesInDownloads()
        {
            using (var tempDir = new TempFolder())
            {
                var downloadsFolder = Path.Combine(tempDir.Dir.FullName, "downloads");
                Directory.CreateDirectory(downloadsFolder);
                File.Create(Path.Combine(tempDir.Dir.FullName, $"downloads/someFile.txt"));
                Assert.IsTrue(MO2Installer.CheckValidInstallPath(tempDir.Dir.FullName, downloadFolder: downloadsFolder).Succeeded);
            }
        }
        #endregion
    }
}
