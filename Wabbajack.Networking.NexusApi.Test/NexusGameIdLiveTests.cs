using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Xunit;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     A manual Nexus download opens Nexus's download page for the file, which is addressed by the file id and the
///     game's numeric id rather than its name (ManualDownloadUrls.NexusDownloadPopup). A wrong id in the registry
///     would send the user to another game's file, or to nothing, with no error anywhere. This compares every id the
///     registry holds with the list Nexus publishes.
/// </summary>
[Collection(NexusCredentialTests.SerialisedAroundTheApiKey)]
public class NexusGameIdLiveTests
{
    private readonly NexusApi _api;

    public NexusGameIdLiveTests(NexusApi api)
    {
        _api = api;
    }

    [Fact]
    [Trait("Category", "RequiresOAuth")]
    public async Task EveryNexusGameIdMatchesWhatNexusPublishes()
    {
        var (games, _) = await _api.Games();
        // Nexus lists a few domains more than once, so a domain is a set of ids rather than one.
        var byDomain = games.ToLookup(g => g.DomainName, g => g.Id, StringComparer.OrdinalIgnoreCase);

        var wrong = GameRegistry.Games.Values
            .Where(meta => meta.NexusGameId > 0 && !string.IsNullOrWhiteSpace(meta.NexusName))
            .Where(meta => !byDomain[meta.NexusName!].Contains(meta.NexusGameId))
            .Select(meta => byDomain[meta.NexusName!].Any()
                ? $"{meta.Game}: registry says {meta.NexusGameId}, Nexus says {string.Join(" or ", byDomain[meta.NexusName!])} for {meta.NexusName}"
                : $"{meta.Game}: Nexus has no game called {meta.NexusName}")
            .ToList();

        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
    }

    /// <summary>The page the owner confirmed by hand: Skyrim Special Edition, file 209150 of mod 266.</summary>
    [Fact]
    [Trait("Category", "RequiresOAuth")]
    public async Task TheConfirmedDownloadPageIsForTheGameNexusSaysItIs()
    {
        var (games, _) = await _api.Games();

        Assert.Equal(games.Single(g => g.DomainName == "skyrimspecialedition").Id,
            Game.SkyrimSpecialEdition.MetaData().NexusGameId);
        Assert.Equal("https://www.nexusmods.com/Core/Libs/Common/Widgets/DownloadPopUp?id=209150&game_id=1704",
            ManualDownloadUrls.NexusDownloadPopup(209150, Game.SkyrimSpecialEdition.MetaData().NexusGameId).AbsoluteUri);
    }
}
