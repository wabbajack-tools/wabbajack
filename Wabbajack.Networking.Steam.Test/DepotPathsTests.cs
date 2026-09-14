using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamKit2;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     The rule for turning "the file the modlist wants" into "the entry in this manifest". A wrong answer
///     here writes the wrong bytes to a game file under the right name, which nothing downstream would
///     catch, so the cases that must not match matter as much as the ones that must.
/// </summary>
public class DepotPathsTests
{
    [Theory]
    [InlineData("Data/Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("\\Data\\Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("Data\\Skyrim.esm\\", "Data\\Skyrim.esm")]
    [InlineData(".\\Data\\Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("  Data/Skyrim.esm  ", "Data\\Skyrim.esm")]
    // Windows reads a run of separators as one, so two paths that differ only in that are one path.
    [InlineData("Data\\\\Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("Data//Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("Data\\/\\Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("", "")]
    public void NormalisingRemovesOnlySpelling(string input, string expected)
    {
        Assert.Equal(expected, DepotPaths.Normalize(input));
    }

    [Theory]
    // The two sides genuinely are written differently: a modlist records a game-relative path with whatever
    // separator and casing it was compiled with, the manifest records Valve's.
    [InlineData("Data/Skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("data\\skyrim.esm", "Data\\Skyrim.esm")]
    [InlineData("installscript.vdf", "InstallScript.VDF")]
    public void PathsThatOnlyDifferInSpellingAreTheSameFile(string a, string b)
    {
        Assert.True(DepotPaths.AreSame(a, b));
    }

    [Theory]
    [InlineData("Data\\Skyrim.esm", "Data\\Skyrim.esp")]
    [InlineData("Data\\Skyrim.esm", "Skyrim.esm")]
    [InlineData("Data\\Skyrim.esm", "Other\\Data\\Skyrim.esm")]
    public void DifferentFilesAreNotTheSameFile(string a, string b)
    {
        Assert.False(DepotPaths.AreSame(a, b));
    }

    [Theory]
    // A depot rooted above the game folder: the manifest path carries a prefix the modlist never saw.
    [InlineData("SkyrimSE\\Data\\Skyrim.esm", "Data/Skyrim.esm", true)]
    [InlineData("Data\\Skyrim.esm", "Data\\Skyrim.esm", true)]
    // Only on a separator boundary. "NotSkyrim.esm" ends with "Skyrim.esm" as text and is a different file.
    [InlineData("Data\\NotSkyrim.esm", "Skyrim.esm", false)]
    [InlineData("Data\\Skyrim.esm", "SkyrimSE\\Data\\Skyrim.esm", false)]
    [InlineData("Data\\Skyrim.esm", "", false)]
    public void SuffixMatchingStopsAtASeparator(string manifestPath, string wanted, bool expected)
    {
        Assert.Equal(expected, DepotPaths.EndsWithPath(manifestPath, wanted));
    }

    [Fact]
    public void AnExactMatchIsPreferredOverASuffixMatch()
    {
        var files = new[]
        {
            File("Data\\Skyrim.esm"),
            File("Backup\\Data\\Skyrim.esm")
        };

        Assert.Equal("Data\\Skyrim.esm", FoundPath(files, "Data/Skyrim.esm"));
    }

    [Fact]
    public void ASuffixMatchIsTakenWhenItIsTheOnlyOne()
    {
        var files = new[]
        {
            File("SkyrimSE\\Data\\Skyrim.esm"),
            File("SkyrimSE\\Data\\Update.esm")
        };

        Assert.Equal("SkyrimSE\\Data\\Skyrim.esm", FoundPath(files, "Data\\Skyrim.esm"));
    }

    [Fact]
    public void ABareFileNameFindsTheOneFileWithThatName()
    {
        // A name with no separators is just a path with no separators, so the suffix rule covers it. Worth
        // pinning because it is the loosest thing the matcher will do.
        var files = new[]
        {
            File("SkyrimSE\\Data\\Skyrim.esm"),
            File("SkyrimSE\\SkyrimSE.exe")
        };

        Assert.Equal("SkyrimSE\\Data\\Skyrim.esm", FoundPath(files, "Skyrim.esm"));
    }

    [Fact]
    public void ABareFileNameSharedByTwoFilesFindsNeither()
    {
        // The uniqueness guard is the only reason matching a bare name is safe at all. Depots repeat names
        // across folders constantly, and picking one of two would write the wrong bytes under the right
        // name -- which nothing downstream would catch.
        var files = new[]
        {
            File("Data\\Textures\\readme.txt"),
            File("Data\\Meshes\\readme.txt")
        };

        Assert.Null(DepotPaths.Find(files, "readme.txt"));
    }

    [Fact]
    public void TwoSuffixMatchesAreAnAmbiguityRatherThanAGuess()
    {
        // Nothing in the request says which of these was meant, and picking one would be inventing an
        // answer. The caller is told nothing matched instead.
        var files = new[]
        {
            File("SkyrimSE\\Data\\Skyrim.esm"),
            File("Backup\\Data\\Skyrim.esm")
        };

        Assert.Null(DepotPaths.Find(files, "Data\\Skyrim.esm"));
    }

    [Fact]
    public void DirectoriesAreNotFiles()
    {
        var files = new[]
        {
            File("Data", EDepotFileFlag.Directory)
        };

        Assert.Null(DepotPaths.Find(files, "Data"));
    }

    [Fact]
    public void NothingMatchingIsNull()
    {
        Assert.Null(DepotPaths.Find(new[] {File("Data\\Skyrim.esm")}, "Data\\Dawnguard.esm"));
    }

    /// <summary>
    ///     The path of the match, spelled the way a depot spells one. SteamKit rewrites
    ///     <see cref="DepotManifest.FileData.FileName" /> to the host's directory separator, so on Linux a
    ///     depot path comes back with forward slashes and a backslash literal would never equal it. The
    ///     matcher's promise is about which file is found, not how the host punctuates it, so compare the
    ///     normalised form.
    /// </summary>
    private static string? FoundPath(IEnumerable<DepotManifest.FileData> files, string wanted)
    {
        var found = DepotPaths.Find(files, wanted);
        return found == null ? null : DepotPaths.Normalize(found.FileName);
    }

    private static DepotManifest.FileData File(string name, EDepotFileFlag flags = 0)
    {
        return new DepotManifest.FileData(name, Encoding.UTF8.GetBytes(name), flags, 0,
            Array.Empty<byte>(), string.Empty, false, 0);
    }
}
