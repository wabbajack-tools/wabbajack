using System;
using System.Linq;
using FluentAssertions;
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

        candidates.First().Should().Be("SteamAPI_SteamUser_v030");
        candidates.Last().Should().Be("SteamAPI_SteamUser_v005");
        candidates.Should()
            .HaveCount(SteamApiInterfaceProbe.HighestVersion - SteamApiInterfaceProbe.LowestVersion + 1);
        candidates.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void VersionsAreThreeDigitsWithLeadingZeroes()
    {
        SteamApiInterfaceProbe.ExportName(SteamApiInterfaceProbe.AppsInterface, 8)
            .Should().Be("SteamAPI_SteamApps_v008");
    }

    [Theory]
    [InlineData(SteamApiInterfaceProbe.UserInterface, "SteamAPI_SteamUser_v021")]
    [InlineData(SteamApiInterfaceProbe.AppsInterface, "SteamAPI_SteamApps_v008")]
    [InlineData(SteamApiInterfaceProbe.UtilsInterface, "SteamAPI_SteamUtils_v010")]
    public void ResolvesWhatSkyrimSpecialEditionShips(string @interface, string expected)
    {
        var exports = FakeSteamApiExports.SkyrimSpecialEdition();

        SteamApiInterfaceProbe.Resolve(exports, @interface, out var resolved).Should().NotBe(IntPtr.Zero);

        resolved.Should().Be(expected);
    }

    [Theory]
    [InlineData(SteamApiInterfaceProbe.UserInterface, "SteamAPI_SteamUser_v023")]
    [InlineData(SteamApiInterfaceProbe.AppsInterface, "SteamAPI_SteamApps_v009")]
    [InlineData(SteamApiInterfaceProbe.UtilsInterface, "SteamAPI_SteamUtils_v011")]
    public void ResolvesWhatACurrentSdkShips(string @interface, string expected)
    {
        var exports = FakeSteamApiExports.CurrentSdk();

        SteamApiInterfaceProbe.Resolve(exports, @interface, out var resolved).Should().NotBe(IntPtr.Zero);

        resolved.Should().Be(expected);
    }

    [Fact]
    public void PrefersTheNewestVersionPresent()
    {
        var exports = new FakeSteamApiExports().Exporting("SteamAPI_SteamUser_v018", "SteamAPI_SteamUser_v021");

        SteamApiInterfaceProbe.Resolve(exports, SteamApiInterfaceProbe.UserInterface, out var resolved);

        resolved.Should().Be("SteamAPI_SteamUser_v021");
    }

    [Fact]
    public void FallsThroughAnAccessorThatHandsBackNothing()
    {
        var exports = new FakeSteamApiExports()
            .ExportingDeclined("SteamAPI_SteamUser_v023")
            .Exporting("SteamAPI_SteamUser_v021");

        SteamApiInterfaceProbe.Resolve(exports, SteamApiInterfaceProbe.UserInterface, out var resolved)
            .Should().NotBe(IntPtr.Zero);

        resolved.Should().Be("SteamAPI_SteamUser_v021");
        exports.Called.Should().Equal("SteamAPI_SteamUser_v023", "SteamAPI_SteamUser_v021");
    }

    [Fact]
    public void ReportsNothingResolvedRatherThanGuessing()
    {
        var exports = new FakeSteamApiExports().Exporting("SteamAPI_SteamUser_v004");

        SteamApiInterfaceProbe.Resolve(exports, SteamApiInterfaceProbe.UserInterface, out var resolved)
            .Should().Be(IntPtr.Zero);

        resolved.Should().BeNull();
    }

    [Fact]
    public void SkyrimSpecialEditionInitialisesThroughTheLegacyEntryPoint()
    {
        SteamApiInterfaceProbe.SelectInit(FakeSteamApiExports.SkyrimSpecialEdition())
            .Should().Be(SteamApiInitKind.Legacy);
    }

    [Fact]
    public void TheFlatEntryPointWinsWhenBothAreExported()
    {
        SteamApiInterfaceProbe.SelectInit(FakeSteamApiExports.CurrentSdk())
            .Should().Be(SteamApiInitKind.Flat);
    }

    [Fact]
    public void NoInitEntryPointIsItsOwnAnswer()
    {
        SteamApiInterfaceProbe.SelectInit(new FakeSteamApiExports().Exporting("SteamAPI_Shutdown"))
            .Should().Be(SteamApiInitKind.None);
    }
}
