using System.Net;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Which depots and manifests a given version of a game was published as.
///     Steam's client API cannot enumerate a depot's history - it only ever says what is published now - so
///     the one way to reach an older build is a manifest id somebody recorded while it was current. That is
///     what <c>wabbajack-tools/indexed-game-files</c> is, and this is the seam over reading it.
/// </summary>
public interface ISteamManifestIndex
{
    /// <summary>
    ///     The depot and manifest pairs recorded for <paramref name="version" /> of <paramref name="game" />,
    ///     or an empty array when the index does not carry that version. Empty is an ordinary answer, not a
    ///     failure: the index holds the versions modlists have actually been built against, and a list can
    ///     name one nobody has indexed.
    /// </summary>
    Task<SteamManifest[]> Get(Game game, string version, CancellationToken token);
}

/// <summary>Reads the index over the Wabbajack client API.</summary>
public class ClientSteamManifestIndex : ISteamManifestIndex
{
    private readonly WabbajackClientApi.Client _client;
    private readonly ILogger<ClientSteamManifestIndex> _logger;

    public ClientSteamManifestIndex(ILogger<ClientSteamManifestIndex> logger, WabbajackClientApi.Client client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task<SteamManifest[]> Get(Game game, string version, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(version)) return Array.Empty<SteamManifest>();

        try
        {
            var manifests = await _client.GetSteamManifests(game, version, token);
            _logger.LogInformation("The index records {Count} depots for {Game} {Version}", manifests.Length,
                game, version);
            return manifests;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The index is a folder of files named after versions, so a version nobody has indexed is a 404
            // rather than an empty document. That is the ordinary "we don't have that build" answer.
            _logger.LogInformation("The index has no entry for {Game} {Version}", game, version);
            return Array.Empty<SteamManifest>();
        }
    }
}
