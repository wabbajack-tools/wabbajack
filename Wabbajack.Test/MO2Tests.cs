using System.IO;
using Wabbajack.Common;
using Wabbajack.Lib;
using Xunit;

namespace Wabbajack.Test
{
    public class MO2Tests
    {
        #region CheckValidInstallPath
        [Fact]
        public void CheckValidInstallPath_Empty()
        {
            using var tempDir = new TempFolder();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public void CheckValidInstallPath_DoesNotExist()
        {
            using var tempDir = new TempFolder();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir.Combine("Subfolder"), downloadFolder: null).Succeeded);
        }

        [Fact]
        public void CheckValidInstallPath_HasModlist()
        {
            using var tempDir = new TempFolder();
            using var mo2 = tempDir.Dir.Combine("ModOrganizer.exe").Create();
            using var molist = tempDir.Dir.Combine(((RelativePath)"modlist")).WithExtension(Consts.ModListExtension).Create();
            Assert.False(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public void CheckValidInstallPath_ProperOverwrite()
        {
            using var tempDir = new TempFolder();
            using var tmp = tempDir.Dir.Combine(Consts.ModOrganizer2Exe).Create();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public void CheckValidInstallPath_ImproperOverwrite()
        {
            using var tempDir = new TempFolder();
            tempDir.Dir.DeleteDirectory();
            tempDir.Dir.CreateDirectory();
            using var tmp = tempDir.Dir.Combine($"someFile.txt").Create();
            Assert.False(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public void CheckValidInstallPath_OverwriteFilesInDownloads()
        {
            using var tempDir = new TempFolder();
            var downloadsFolder = tempDir.Dir.Combine("downloads");
            downloadsFolder.CreateDirectory();
            using var tmp = tempDir.Dir.Combine($"downloads/someFile.txt").Create();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: downloadsFolder).Succeeded);
        }
        #endregion
    }
}
