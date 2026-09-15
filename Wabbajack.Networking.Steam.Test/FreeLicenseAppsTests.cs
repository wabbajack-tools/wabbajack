using System;
using System.Linq;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     The one thing a game file repair does to the user's Steam account rather than their disk. Both
///     directions matter: saying nothing when a free tool licence is about to be taken hides it, and saying
///     it on every repair trains people to scroll past the one time it is true.
/// </summary>
public class FreeLicenseAppsTests
{
    private const uint SkyrimSE = 489830;
    private const uint SkyrimSECreationKit = 1946180;

    [Fact]
    public void AGameTheUserAlreadyOwnsCostsThemNothing()
    {
        Assert.False(FreeLicenseApps.NeedsFreeLicense(SkyrimSE));
        Assert.Empty(FreeLicenseApps.Describe(new[] {SkyrimSE}));
    }

    [Fact]
    public void NothingAtAllIsStillNothing()
    {
        Assert.Empty(FreeLicenseApps.Describe(Array.Empty<uint>()));
    }

    [Fact]
    public void AFreeToolSaysItGoesIntoTheLibraryAndNamesIt()
    {
        Assert.True(FreeLicenseApps.NeedsFreeLicense(SkyrimSECreationKit));

        var said = Assert.Single(FreeLicenseApps.Describe(new[] {SkyrimSE, SkyrimSECreationKit}));

        Assert.Contains("Creation Kit", said);
        Assert.Contains("Steam library", said);
    }

    /// <summary>
    ///     One sentence however many tools are in the repair, and the app named once however many of its
    ///     files are: the user is agreeing to one thing.
    /// </summary>
    [Fact]
    public void TheSameToolTwiceIsSaidOnce()
    {
        var said = Assert.Single(
            FreeLicenseApps.Describe(new[] {SkyrimSECreationKit, SkyrimSECreationKit, SkyrimSE}));

        Assert.Equal(1, said.Split("Creation Kit").Length - 1);
    }
}
