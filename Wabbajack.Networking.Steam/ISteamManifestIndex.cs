using System.Net;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     What <c>wabbajack-tools/indexed-game-files</c> knows, and the seam over reading it.
///     Steam's client API cannot enumerate a depot's history - it only ever says what is published now - so
///     everything about an older build comes from something somebody recorded while they had it.
///     <para>
///         Two questions, and the second is the better one. <see cref="Get" /> asks which manifests a
///         version was published as, which only helps when the exact version string a modlist recorded is
///         one somebody indexed. <see cref="Find" /> asks which manifest carries a file with these bytes,
///         which is the question a repair actually has: a modlist identifies a game file by hash, and the
///         hash is true whatever the build is called.
///     </para>
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

    /// <summary>
    ///     Every recorded copy of the file that hashes to <paramref name="hash" />: which app, depot and
    ///     manifest carry it, and what it is called in there. Usually one, sometimes several - a file that
    ///     did not change between builds is in every manifest of them - and empty when nobody has indexed a
    ///     build containing it, which is the ordinary answer for a game nobody has got round to.
    /// </summary>
    Task<IndexedGameFile[]> Find(Game game, Hash hash, CancellationToken token);
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

    public async Task<IndexedGameFile[]> Find(Game game, Hash hash, CancellationToken token)
    {
        try
        {
            // A shard holds every indexed file whose hash starts with the same byte, so the answer has to
            // be picked out of it; this is the only place that knows the file it asked for.
            var shard = await _client.GetIndexedGameFiles(game, hash, token);
            var found = shard.Where(f => f.Hash == hash).ToArray();

            _logger.LogInformation("The content index has {Count} copies of {Hash} for {Game}", found.Length,
                hash, game);
            return found;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Nobody has indexed a build of this game whose files land in that shard.
            _logger.LogInformation("The content index has nothing for {Game} in shard {Shard}", game,
                GameFileIndex.ShardOf(hash));
            return Array.Empty<IndexedGameFile>();
        }
    }
}
