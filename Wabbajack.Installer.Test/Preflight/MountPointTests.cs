using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>Which file system a Unix folder is on, for the disk-space check.</summary>
public class MountPointTests
{
    private static readonly string[] Mounts = {"/", "/home", "/mnt/games", "/mnt/games/fast", "/proc"};

    [Theory]
    [InlineData("/home/me/Lists/Tiny", "/home")]
    [InlineData("/mnt/games/Lists", "/mnt/games")]
    [InlineData("/mnt/games/fast/Lists", "/mnt/games/fast")]
    [InlineData("/mnt/gamesextra/Lists", "/")]
    [InlineData("/home", "/home")]
    [InlineData("/opt/Wabbajack", "/")]
    public void TheDeepestMountPointHoldingTheFolderWins(string folder, string expected)
    {
        Assert.Equal(expected, Volumes.MountPointOf(folder, Mounts));
    }

    [Fact]
    public void ATrailingSlashOnAMountPointIsIgnored()
    {
        Assert.Equal("/home/", Volumes.MountPointOf("/home/me", new[] {"/", "/home/"}));
    }
}
