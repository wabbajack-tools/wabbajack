#nullable enable
using System;
using System.Collections.Generic;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.Installer.Preflight.Rules;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class BannedSourcesTests
{
    private static readonly IReadOnlyList<string> Bans = BannedSources.Normalize(new[] {"modholdmods.com"});

    [Theory]
    [InlineData("modholdmods.com", "modholdmods.com")]
    [InlineData("  ModHoldMods.com ", "modholdmods.com")]
    [InlineData("https://www.modholdmods.com/files/", "modholdmods.com")]
    [InlineData("*.modholdmods.com", "modholdmods.com")]
    [InlineData("www.modholdmods.com", "modholdmods.com")]
    [InlineData("modholdmods.com/some/path", "modholdmods.com")]
    public void EntriesAreReadAsHosts(string entry, string host)
    {
        Assert.Equal(new[] {host}, BannedSources.Normalize(new[] {entry}));
    }

    [Fact]
    public void EmptyAndMissingEntriesAreIgnored()
    {
        Assert.Empty(BannedSources.Normalize(null));
        Assert.Empty(BannedSources.Normalize(new[] {"", "   ", null}));
        Assert.Single(BannedSources.Normalize(new[] {"a.com", "A.com", "www.a.com"}));
    }

    [Theory]
    [InlineData("modholdmods.com", true)]
    [InlineData("MODHOLDMODS.COM", true)]
    [InlineData("www.modholdmods.com", true)]
    [InlineData("cdn.files.modholdmods.com", true)]
    [InlineData("modholdmods.com.", true)]
    [InlineData("notmodholdmods.com", false)]
    [InlineData("modholdmods.com.evil.net", false)]
    [InlineData("modholdmods.org", false)]
    public void ABanCoversTheHostAndItsSubdomains(string host, bool banned)
    {
        Assert.Equal(banned, BannedSources.Matches(host, "modholdmods.com"));
    }

    public static IEnumerable<object[]> States()
    {
        var banned = new Uri("https://www.modholdmods.com/download/file.7z");
        var fine = new Uri("https://github.com/someone/file.7z");
        yield return new object[] {new Http {Url = banned}, true};
        yield return new object[] {new Http {Url = fine}, false};
        // A manual download is exactly what the allow-list never looks at during an install.
        yield return new object[] {new Manual {Url = banned, Prompt = "Click download"}, true};
        yield return new object[] {new Manual {Url = fine}, false};
        yield return new object[] {new MediaFire {Url = new Uri("https://modholdmods.com/mf/abc")}, true};
        yield return new object[] {new Mega {Url = new Uri("https://mega.nz/file/abc")}, false};
        yield return new object[] {new ModDB {Url = new Uri("https://cdn.modholdmods.com/x")}, true};
        yield return new object[] {new WabbajackCDN {Url = new Uri("https://authored-files.wabbajack.org/x")}, false};
        yield return new object[] {new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 1, FileID = 2}, false};
        yield return new object[] {new GoogleDrive {Id = "abc"}, false};
        yield return new object[] {new GameFileSource {Game = Game.SkyrimSpecialEdition}, false};
    }

    [Theory]
    [MemberData(nameof(States))]
    public void EveryKindOfSourceIsAskedAboutItsAddress(IDownloadState state, bool banned)
    {
        Assert.Equal(banned ? "modholdmods.com" : null, BannedSources.BanFor(state, Bans));
    }

    [Fact]
    public void NoBansBanNothing()
    {
        Assert.Null(BannedSources.BanFor(new Http {Url = new Uri("https://modholdmods.com/a")},
            Array.Empty<string>()));
    }
}
