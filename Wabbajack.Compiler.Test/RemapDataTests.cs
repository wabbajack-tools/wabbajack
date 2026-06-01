using Wabbajack.Compiler.CompilationSteps;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class RemapDataTests
{
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

    [Fact]
    public void MO2ByteArray_SourcePath_InGamePathField_RemappedWithStockGameFolderSuffix()
    {
        // MO2 on Linux under Wine stores paths in @ByteArray with Z:\\ prefix (double backslash
        // after the drive letter), which @ByteArray-encodes to 4 backslashes before the first
        // path component and 2 backslashes between subsequent components.
        //
        // The gamePath field points to a Stock Game Folder INSIDE the MO2 source directory.
        // The source path prefix should be tokenised to MO2_PATH_MAGIC, leaving the
        // \\Stock Game Folder suffix intact — matching the behaviour of a Windows compile.
        var sourcePath = "/home/user/Games/LitR";
        var data = @"gamePath=@ByteArray(Z:\\\\home\\user\\Games\\LitR\\Stock Game Folder)";

        // gamePath arg is the game install (Steam) — it won't match the @ByteArray Stock Game
        // Folder path, so source-path magic should handle it instead.
        var result = IncludeStubbedConfigFiles.RemapData(data, "/game/steam/Fallout4", sourcePath, "/downloads");

        Assert.Contains(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, result);
        Assert.Contains(@"\\Stock Game Folder", result);
        Assert.DoesNotContain("Z:", result);
    }
}
