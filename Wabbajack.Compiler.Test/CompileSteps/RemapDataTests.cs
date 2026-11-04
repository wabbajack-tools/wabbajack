using Wabbajack.Compiler.CompilationSteps;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Compiler.Test.CompileSteps;

/// <summary>
///     Config files name the author's game, MO2 and downloads folders, and the compiler swaps those for markers. On
///     Linux the same folder is written two ways: as Wine shows it to MO2 and the game's tools (Z:\home\...), and as
///     a native tool writes it (/home/...).
/// </summary>
public class RemapDataTests
{
    private static readonly AbsolutePath Game = (AbsolutePath) "/home/me/.steam/steam/steamapps/common/Skyrim Special Edition";
    private static readonly AbsolutePath Mo2 = (AbsolutePath) "/home/me/Lists/Tiny";
    private static readonly AbsolutePath Downloads = (AbsolutePath) "/home/me/Lists/Tiny/downloads";

    private static string Remap(string data)
    {
        return IncludeStubbedConfigFiles.RemapData(data, Game, Mo2, Downloads);
    }

    [Fact]
    public void WinePathsWithBackslashesBecomeBackMarkers()
    {
        Assert.Equal($"{Consts.MO2_PATH_MAGIC_BACK}\\mods\\SKSE",
            Remap(@"Z:\home\me\Lists\Tiny\mods\SKSE"));
    }

    [Fact]
    public void WinePathsWithDoubledBackslashesBecomeDoubleBackMarkers()
    {
        Assert.Equal($"gamePath=@ByteArray({Consts.GAME_PATH_MAGIC_DOUBLE_BACK})",
            Remap(@"gamePath=@ByteArray(Z:\\home\\me\\.steam\\steam\\steamapps\\common\\Skyrim Special Edition)"));
    }

    [Fact]
    public void WinePathsWithForwardSlashesBecomeForwardMarkers()
    {
        Assert.Equal($"download_directory={Consts.DOWNLOAD_PATH_MAGIC_FORWARD}",
            Remap("download_directory=Z:/home/me/Lists/Tiny/downloads"));
    }

    [Fact]
    public void NativeUnixPathsBecomeForwardMarkers()
    {
        Assert.Equal($"{Consts.MO2_PATH_MAGIC_FORWARD}/overwrite", Remap("/home/me/Lists/Tiny/overwrite"));
    }

    [Fact]
    public void TheDownloadsFolderInsideTheMo2FolderKeepsItsOwnMarker()
    {
        Assert.Equal($"{Consts.DOWNLOAD_PATH_MAGIC_BACK}\\a.7z", Remap(@"Z:\home\me\Lists\Tiny\downloads\a.7z"));
    }

    [Fact]
    public void WindowsPathsAreUnchanged()
    {
        var game = (AbsolutePath) @"C:\Games\Skyrim";
        Assert.Equal($"{Consts.GAME_PATH_MAGIC_FORWARD}/Data",
            IncludeStubbedConfigFiles.RemapData("C:/Games/Skyrim/Data", game, (AbsolutePath) @"C:\MO2",
                (AbsolutePath) @"C:\MO2\downloads"));
    }
}
