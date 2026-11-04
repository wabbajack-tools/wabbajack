using System;
using System.Linq;
using Wabbajack.App.Avalonia.Services;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Services;

/// <summary>Which release's launcher the app installs over the one it was started by.</summary>
public class LauncherReleasesTests
{
    private static LauncherReleases.Asset Asset(string name, string? digest = "sha256:ABCD") =>
        new(name, new Uri("https://example.invalid/" + name), digest);

    private static readonly LauncherReleases.Release[] Releases =
    [
        new("4.4.1.0-beta.1", true, [Asset("Wabbajack.exe", "sha256:BETA"), Asset("Wabbajack-linux-x64.tar.gz", "sha256:BETA")]),
        new("4.4.0.0", false, [Asset("4.4.0.0.zip"), Asset("Wabbajack.exe", "sha256:W440"), Asset("Wabbajack-linux-x64.tar.gz", "sha256:L440")]),
        new("4.3.1.0", false, [Asset("4.3.1.0.zip"), Asset("Wabbajack.exe", "sha256:W431")])
    ];

    [Theory]
    [InlineData("Wabbajack.exe", "w440")]
    [InlineData("Wabbajack-linux-x64.tar.gz", "l440")]
    public void TheNewestStableReleasesLauncherForThisPlatform(string asset, string sha256)
    {
        var launcher = LauncherReleases.Newest(Releases, asset);

        Assert.Equal((new Version(4, 4, 0, 0), sha256), (launcher!.Version, launcher.Sha256));
    }

    /// <summary>A release whose launcher has no digest is passed over: without one there is nothing to compare.</summary>
    [Fact]
    public void ALauncherWithoutADigestIsPassedOver()
    {
        var releases = Releases.Select(r => r.Tag == "4.4.0.0"
            ? r with {Assets = r.Assets.Select(a => a with {Digest = null}).ToList()}
            : r);

        Assert.Equal(new Version(4, 3, 1, 0), LauncherReleases.Newest(releases, "Wabbajack.exe")!.Version);
        Assert.Null(LauncherReleases.Newest(releases, "Wabbajack-linux-x64.tar.gz"));
    }
}
