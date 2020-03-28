using System.IO;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Xunit;

namespace Wabbajack.Test
{
    public class MO2Tests
    {
        #region CheckValidInstallPath
        [Fact]
        public async Task CheckValidInstallPath_Empty()
        {
            await using var tempDir = new TempFolder();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public async Task CheckValidInstallPath_DoesNotExist()
        {
            await using var tempDir = new TempFolder();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir.Combine("Subfolder"), downloadFolder: null).Succeeded);
        }

        [Fact]
        public async Task CheckValidInstallPath_HasModlist()
        {
            await using var tempDir = new TempFolder();
            await using var mo2 = tempDir.Dir.Combine("ModOrganizer.exe").Create();
            await using var molist = tempDir.Dir.Combine(((RelativePath)"modlist")).WithExtension(Consts.ModListExtension).Create();
            Assert.False(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public async Task CheckValidInstallPath_ProperOverwrite()
        {
            await using var tempDir = new TempFolder();
            await using var tmp = tempDir.Dir.Combine(Consts.ModOrganizer2Exe).Create();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public async Task CheckValidInstallPath_ImproperOverwrite()
        {
            await using var tempDir = new TempFolder();
            await tempDir.Dir.DeleteDirectory();
            tempDir.Dir.CreateDirectory();
            await using var tmp = tempDir.Dir.Combine($"someFile.txt").Create();
            Assert.False(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: null).Succeeded);
        }

        [Fact]
        public async Task CheckValidInstallPath_OverwriteFilesInDownloads()
        {
            await using var tempDir = new TempFolder();
            var downloadsFolder = tempDir.Dir.Combine("downloads");
            downloadsFolder.CreateDirectory();
            await using var tmp = tempDir.Dir.Combine($"downloads/someFile.txt").Create();
            Assert.True(MO2Installer.CheckValidInstallPath(tempDir.Dir, downloadFolder: downloadsFolder).Succeeded);
        }
        #endregion
    }
}
