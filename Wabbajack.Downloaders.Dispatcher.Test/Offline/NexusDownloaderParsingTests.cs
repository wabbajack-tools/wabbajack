using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test.Offline;

/// <summary>
///     How a Nexus Mods link or an MO2 meta file becomes a download state. None of it talks to Nexus, so the
///     downloader is built without an API client.
/// </summary>
public class NexusDownloaderParsingTests
{
    private static readonly NexusDownloader Downloader = new(NullLogger<NexusDownloader>.Instance, null!, null!);

    [Theory]
    [InlineData("https://www.nexusmods.com/skyrimspecialedition/mods/51939?tab=files&file_id=212497",
        Game.SkyrimSpecialEdition, 51939, 212497)]
    [InlineData("https://www.nexusmods.com/fallout4/mods/12345/?tab=files&file_id=678",
        Game.Fallout4, 12345, 678)]
    public void Parse_ReadsGameModAndFile(string url, Game game, long modId, long fileId)
    {
        var state = Assert.IsType<Nexus>(Downloader.Parse(new Uri(url)));

        Assert.Equal(game, state.Game);
        Assert.Equal(modId, state.ModID);
        Assert.Equal(fileId, state.FileID);
    }

    [Theory]
    [InlineData("https://nexusmods.com/skyrimspecialedition/mods/51939?file_id=1")]
    [InlineData("https://www.nexusmods.com/skyrimspecialedition/mods/51939")]
    [InlineData("https://www.nexusmods.com/skyrimspecialedition/mods/notanumber?file_id=1")]
    [InlineData("https://www.nexusmods.com/notagame/mods/51939?file_id=1")]
    [InlineData("https://www.nexusmods.com/skyrimspecialedition/mods/51939/files/2?file_id=1")]
    public void Parse_AnythingElse_IsNotNexus(string url)
    {
        Assert.Null(Downloader.Parse(new Uri(url)));
    }

    [Fact]
    public void UnParse_ParsesBackToTheSameState()
    {
        var state = new Nexus {Game = Game.SkyrimSpecialEdition, ModID = 51939, FileID = 212497};

        var again = Assert.IsType<Nexus>(Downloader.Parse(Downloader.UnParse(state)));

        Assert.Equal((state.Game, state.ModID, state.FileID), (again.Game, again.ModID, again.FileID));
    }

    [Theory]
    [InlineData("SkyrimSE", Game.SkyrimSpecialEdition)]
    [InlineData("skyrimspecialedition", Game.SkyrimSpecialEdition)]
    [InlineData("Fallout4", Game.Fallout4)]
    public void Resolve_AcceptsTheMo2OrTheNexusGameName(string gameName, Game game)
    {
        var state = Assert.IsType<Nexus>(Downloader.Resolve(new Dictionary<string, string>
        {
            {"gameName", gameName}, {"modID", "51939"}, {"fileID", "212497"}
        }));

        Assert.Equal(game, state.Game);
        Assert.Equal(51939, state.ModID);
        Assert.Equal(212497, state.FileID);
    }

    [Theory]
    [InlineData("", "1", "2")]
    [InlineData("SkyrimSE", "", "2")]
    [InlineData("SkyrimSE", "1", " ")]
    public void Resolve_WithAFieldMissing_IsNotNexus(string gameName, string modId, string fileId)
    {
        Assert.Null(Downloader.Resolve(new Dictionary<string, string>
        {
            {"gameName", gameName}, {"modID", modId}, {"fileID", fileId}
        }));
    }
}
