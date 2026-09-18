using System;
using System.Linq;
using Wabbajack.Networking.Bethesda.Steam;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The probe is the part that has to be right about two SDK generations at once, and the part with the
///     least to go on at runtime, so the two real export tables are pinned here as a table of their own.
/// </summary>
public class SteamApiInterfaceProbeTests
{
    [Fact]
    public void CandidatesDescendFromTheNewestVersion()
    {
        var candidates = SteamApiInterfaceProbe.Candidates(SteamApiInterfaceProbe.UserInterface).ToList();

        Assert.Equal("SteamAPI_SteamUser_v030", candidates.First());
        Assert.Equal("SteamAPI_SteamUser_v005", candidates.Last());
        Assert.Equal(SteamApiInterfaceProbe.HighestVersion - SteamApiInterfaceProbe.LowestVersion + 1,
            candidates.Count);
        Assert.Equal(candidates.Count, candidates.Distinct().Count());
    }

    [Fact]
    public void VersionsAreThreeDigitsWithLeadingZeroes()
    {
        Assert.Equal("SteamAPI_SteamApps_v008",
            SteamApiInterfaceProbe.ExportName(SteamApiInterfaceProbe.AppsInterface, 8));
    }

    [Theory]
    [InlineData(SteamApiInterfaceProbe.UserInterface, "SteamAPI_SteamUser_v021")]
    [InlineData(SteamApiInterfaceProbe.AppsInterface, "SteamAPI_SteamApps_v008")]
    [InlineData(SteamApiInterfaceProbe.UtilsInterface, "SteamAPI_SteamUtils_v010")]
    public void ResolvesWhatSkyrimSpecialEditionShips(string @interface, string expected)
    {
        var exports = FakeSteamApiExports.SkyrimSpecialEdition();

        Assert.NotEqual(IntPtr.Zero, SteamApiInterfaceProbe.Resolve(exports, @interface, out var resolved));

        Assert.Equal(expected, resolved);
    }

    [Theory]
    [InlineData(SteamApiInterfaceProbe.UserInterface, "SteamAPI_SteamUser_v023")]
    [InlineData(SteamApiInterfaceProbe.AppsInterface, "SteamAPI_SteamApps_v009")]
    [InlineData(SteamApiInterfaceProbe.UtilsInterface, "SteamAPI_SteamUtils_v011")]
    public void ResolvesWhatACurrentSdkShips(string @interface, string expected)
    {
        var exports = FakeSteamApiExports.CurrentSdk();

        Assert.NotEqual(IntPtr.Zero, SteamApiInterfaceProbe.Resolve(exports, @interface, out var resolved));

        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void PrefersTheNewestVersionPresent()
    {
        var exports = new FakeSteamApiExports().Exporting("SteamAPI_SteamUser_v018", "SteamAPI_SteamUser_v021");

        SteamApiInterfaceProbe.Resolve(exports, SteamApiInterfaceProbe.UserInterface, out var resolved);

        Assert.Equal("SteamAPI_SteamUser_v021", resolved);
    }

    [Fact]
    public void FallsThroughAnAccessorThatHandsBackNothing()
    {
        var exports = new FakeSteamApiExports()
            .ExportingDeclined("SteamAPI_SteamUser_v023")
            .Exporting("SteamAPI_SteamUser_v021");

        Assert.NotEqual(IntPtr.Zero,
            SteamApiInterfaceProbe.Resolve(exports, SteamApiInterfaceProbe.UserInterface, out var resolved));

        Assert.Equal("SteamAPI_SteamUser_v021", resolved);
        Assert.Equal(new[] {"SteamAPI_SteamUser_v023", "SteamAPI_SteamUser_v021"}, exports.Called);
    }

    [Fact]
    public void ReportsNothingResolvedRatherThanGuessing()
    {
        var exports = new FakeSteamApiExports().Exporting("SteamAPI_SteamUser_v004");

        Assert.Equal(IntPtr.Zero,
            SteamApiInterfaceProbe.Resolve(exports, SteamApiInterfaceProbe.UserInterface, out var resolved));

        Assert.Null(resolved);
    }

    [Fact]
    public void SkyrimSpecialEditionInitialisesThroughTheLegacyEntryPoint()
    {
        Assert.Equal(SteamApiInitKind.Legacy,
            SteamApiInterfaceProbe.SelectInit(FakeSteamApiExports.SkyrimSpecialEdition()));
    }

    [Fact]
    public void TheFlatEntryPointWinsWhenBothAreExported()
    {
        Assert.Equal(SteamApiInitKind.Flat, SteamApiInterfaceProbe.SelectInit(FakeSteamApiExports.CurrentSdk()));
    }

    [Fact]
    public void NoInitEntryPointIsItsOwnAnswer()
    {
        Assert.Equal(SteamApiInitKind.None,
            SteamApiInterfaceProbe.SelectInit(new FakeSteamApiExports().Exporting("SteamAPI_Shutdown")));
    }
}
