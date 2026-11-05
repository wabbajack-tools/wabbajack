#nullable enable
using System;
using System.IO;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

/// <summary>
///     <see cref="WinePrefixes.Resolve" /> against prefixes laid out as Wine lays them out: a dosdevices folder of
///     links, c: relative to it and others absolute. Only meaningful where those links can be made.
/// </summary>
public class WinePrefixesTests : IDisposable
{
    private readonly string _root = Path.Combine(KnownFolders.EntryPoint.ToString(), "wineprefixes-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    /// <summary>A prefix with a C: drive, and optionally a file in its steamuser's Downloads folder.</summary>
    private AbsolutePath Prefix(string name, bool downloadsHoldSomething)
    {
        var prefix = Path.Combine(_root, name, "pfx");
        var downloads = Path.Combine(prefix, "drive_c", "users", "steamuser", "Downloads");
        Directory.CreateDirectory(downloads);
        Directory.CreateDirectory(Path.Combine(prefix, "dosdevices"));
        Directory.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "c:"), "../drive_c");
        if (downloadsHoldSomething) File.WriteAllText(Path.Combine(downloads, "SkyUI.7z"), "data");
        return prefix.ToAbsolutePath();
    }

    [Fact]
    public void AC_Path_IsFoundThroughThePrefixWhereItHoldsSomething()
    {
        if (OperatingSystem.IsWindows()) return;
        var empty = Prefix("empty", false);
        var used = Prefix("used", true);

        var found = WinePrefixes.Resolve(@"C:\users\steamuser\Downloads", new[] {empty, used});

        Assert.NotNull(found);
        Assert.Equal(used, found!.Value.Prefix);
        Assert.Equal(used.Combine("drive_c", "users", "steamuser", "Downloads"), found.Value.Path);
    }

    /// <summary>Windows ignores case, and so does a program that wrote the path; the disk keeps its own.</summary>
    [Fact]
    public void ThePath_IsMatchedWithoutCase()
    {
        if (OperatingSystem.IsWindows()) return;
        var prefix = Prefix("one", true);

        var found = WinePrefixes.Resolve(@"c:\Users\SteamUser\DOWNLOADS\skyui.7z", new[] {prefix});

        Assert.Equal(prefix.Combine("drive_c", "users", "steamuser", "Downloads", "SkyUI.7z"), found!.Value.Path);
    }

    /// <summary>Proton's S: is an absolute link to the Steam folder.</summary>
    [Fact]
    public void ADriveLinkedAbsolutely_IsFollowed()
    {
        if (OperatingSystem.IsWindows()) return;
        var prefix = Prefix("one", false);
        var steam = Path.Combine(_root, "Steam");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        Directory.CreateSymbolicLink(Path.Combine(prefix.ToString(), "dosdevices", "s:"), steam);

        var found = WinePrefixes.Resolve(@"S:\steamapps", new[] {prefix});

        Assert.Equal(Path.Combine(steam, "steamapps").ToAbsolutePath(), found!.Value.Path);
    }

    [Theory]
    [InlineData(@"C:\users\nobody\Downloads")]
    [InlineData(@"D:\Modding")]
    [InlineData("/home/me/downloads")]
    [InlineData("downloads")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingNoPrefixHas_IsNull(string? path)
    {
        if (OperatingSystem.IsWindows()) return;
        var prefix = Prefix("one", true);

        Assert.Null(WinePrefixes.Resolve(path, new[] {prefix}));
    }
}
