using System;
using System.Diagnostics;
using System.IO;
using Wabbajack.Common;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.FileExtractor.Test.Offline;

/// <summary>
///     The version resource read straight from a PE file, which is how a game's version is found off Windows.
///     The Windows tools shipped for extraction make good samples: 7z.exe's version string is not its fixed version.
/// </summary>
public class ExecutableVersionTests
{
    private static string Tool(string name)
    {
        return Path.Combine(KnownFolders.EntryPoint.ToString(), "Extractors", "windows-x64", name);
    }

    [Fact]
    public void ReadsTheVersionStringsRatherThanTheFixedVersion()
    {
        var version = ExecutableVersion.ReadPortable(Tool("7z.exe"));

        Assert.Equal("26.01", version.FileVersion);
        Assert.Equal("26.01", version.ProductVersion);
    }

    [Fact]
    public void AnExecutableWithoutAVersionHasNone()
    {
        Assert.Equal(new ExecutableVersion(null, null), ExecutableVersion.ReadPortable(Tool("innoextract.exe")));
    }

    [Fact]
    public void SomethingThatIsNotAnExecutableHasNone()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not a PE file");
            Assert.Equal(new ExecutableVersion(null, null), ExecutableVersion.ReadPortable(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AgreesWithWindowsOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        foreach (var name in new[] {"7z.exe", "7z.dll", "innoextract.exe"})
        {
            var windows = FileVersionInfo.GetVersionInfo(Tool(name));
            var portable = ExecutableVersion.ReadPortable(Tool(name));
            Assert.Equal(string.IsNullOrEmpty(windows.FileVersion) ? null : windows.FileVersion, portable.FileVersion);
            Assert.Equal(string.IsNullOrEmpty(windows.ProductVersion) ? null : windows.ProductVersion,
                portable.ProductVersion);
        }
    }
}
