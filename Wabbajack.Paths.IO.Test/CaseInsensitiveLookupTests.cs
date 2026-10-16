using System;
using System.IO;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

/// <summary>
///     Windows answers these from the file system itself, so they say something only on a case-sensitive one;
///     CI runs them on Linux.
/// </summary>
public class CaseInsensitiveLookupTests : IDisposable
{
    private readonly AbsolutePath _root;

    public CaseInsensitiveLookupTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wj-case-" + Guid.NewGuid().ToString("N")).ToAbsolutePath();
        _root.Combine("Data", "Textures").CreateDirectory();
        File.WriteAllText(_root.Combine("Data", "Textures", "Rock.dds").ToString(), "x");
    }

    public void Dispose()
    {
        Directory.Delete(_root.ToString(), true);
    }

    private static void AssertSpelled(string expected, AbsolutePath actual)
    {
        if (OperatingSystem.IsWindows()) Assert.Equal(expected.ToAbsolutePath(), actual);
        else Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void FindsAFileSpelledAnotherWay()
    {
        var found = _root.FindIgnoringCase((RelativePath) @"data\textures\ROCK.DDS");

        Assert.True(found.FileExists());
        AssertSpelled(_root.Combine("Data", "Textures", "Rock.dds").ToString(), found);
    }

    [Fact]
    public void KeepsTheGivenSpellingForWhatIsNotThere()
    {
        var found = _root.FindIgnoringCase((RelativePath) @"data\Meshes\rock.nif");

        Assert.False(found.FileExists());
        AssertSpelled(_root.Combine("Data", "Meshes", "rock.nif").ToString(), found);
    }

    [Fact]
    public void AnExactSpellingIsReturnedAsItIs()
    {
        AssertSpelled(_root.Combine("Data", "Textures").ToString(),
            _root.FindIgnoringCase((RelativePath) @"Data\Textures"));
    }
}
