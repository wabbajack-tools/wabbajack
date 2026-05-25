using Wabbajack.Compiler.CompilationSteps;
using Xunit;

namespace Wabbajack.Compiler.Test;

/// <summary>
/// Tests for IncludeStubbedConfigFiles.RemapData, which replaces absolute paths in MO2 config
/// files with portable magic strings during compilation. On Linux, MO2 runs under Wine/Proton
/// and stores paths using the Wine Z: drive, so RemapData must handle that format.
/// </summary>
public class RemapDataTests
{
    // On Linux, MO2 runs under Wine/Proton. Wine maps the Linux filesystem root to the Z: drive.
    // So a Linux source path like /home/user/modding appears in modorganizer.ini as Z:\home\user\modding
    // (backslash form) or Z:\\home\\user\\modding (double-backslash Qt INI escaped form).
    // RemapData must replace these Wine Z: path variants with the appropriate magic strings.

    [Fact]
    public void WineZPath_BackslashForm_IsRemappedToMO2Magic()
    {
        var sourcePath = "/home/user/modding";
        var data = @"binary=Z:\home\user\modding\mods\F4SE\root\f4se_loader.exe";

        var result = IncludeStubbedConfigFiles.RemapData(data, "/game/path", sourcePath, "/downloads");

        Assert.Contains(Consts.MO2_PATH_MAGIC_BACK, result);
        Assert.DoesNotContain("Z:\\home\\user\\modding", result);
        Assert.DoesNotContain(@"Z:\home\user\modding", result);
    }

    [Fact]
    public void WineZPath_DoubleBackslashQtIniForm_IsRemappedToMO2Magic()
    {
        var sourcePath = "/home/user/modding";
        // Qt INI format escapes backslashes — this is what modorganizer.ini actually contains
        var data = @"binary=Z:\\home\\user\\modding\\mods\\F4SE\\root\\f4se_loader.exe";

        var result = IncludeStubbedConfigFiles.RemapData(data, "/game/path", sourcePath, "/downloads");

        Assert.Contains(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, result);
        Assert.DoesNotContain(@"Z:\\home\\user\\modding", result);
    }

    [Fact]
    public void WineZPath_ForwardSlashForm_IsRemappedToMO2Magic()
    {
        var sourcePath = "/home/user/modding";
        var data = "binary=Z:/home/user/modding/mods/F4SE/root/f4se_loader.exe";

        var result = IncludeStubbedConfigFiles.RemapData(data, "/game/path", sourcePath, "/downloads");

        Assert.Contains(Consts.MO2_PATH_MAGIC_FORWARD, result);
        Assert.DoesNotContain("Z:/home/user/modding", result);
    }

    [Fact]
    public void WineZPath_GamePath_IsRemappedToGameMagic()
    {
        var gamePath = "/home/user/.steam/common/Fallout4";
        var data = @"gamePath=Z:\home\user\.steam\common\Fallout4";

        var result = IncludeStubbedConfigFiles.RemapData(data, gamePath, "/source", "/downloads");

        Assert.Contains(Consts.GAME_PATH_MAGIC_BACK, result);
        Assert.DoesNotContain(@"Z:\home\user\.steam\common\Fallout4", result);
    }

    [Fact]
    public void WindowsStylePath_StillRemapped_WhenSourceIsWindowsFormat()
    {
        // Control case: on Windows, source is a Windows path; replacement must still work
        var sourcePath = @"C:\modding";
        var data = @"binary=C:\modding\mods\F4SE\root\f4se_loader.exe";

        var result = IncludeStubbedConfigFiles.RemapData(data, @"C:\game", sourcePath, @"C:\downloads");

        Assert.Contains(Consts.MO2_PATH_MAGIC_BACK, result);
        Assert.DoesNotContain(@"C:\modding\mods", result);
    }

    [Fact]
    public void UnrelatedPath_IsNotModified()
    {
        var data = @"binary=D:\other\path\f4se_loader.exe";

        var result = IncludeStubbedConfigFiles.RemapData(data, "/game", "/source", "/downloads");

        Assert.Equal(data, result);
    }
}
