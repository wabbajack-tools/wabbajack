using System.Linq;
using Wabbajack.DTOs;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     Turns a Steam app id back into the game Wabbajack knows by that id.
///     <para>
///         Minting a ticket is asked for by app id, because that is what the Steamworks environment wants and
///         what the API on the other end is keyed by, but it is answered out of a game folder: the library
///         that does the minting is the one that game shipped. This is the one step between the two.
///     </para>
///     <para>
///         Only <see cref="GameMetaData.SteamIDs" /> is searched, never <see cref="GameMetaData.SteamToolIDs" />.
///         A tool app shares the game's folder but is not a thing an account is asked to vouch for.
///     </para>
/// </summary>
public static class SteamAppGames
{
    public static GameMetaData? ForAppId(uint appId)
    {
        return GameRegistry.Games.Values.FirstOrDefault(game => game.SteamIDs.Contains((int) appId));
    }

    /// <summary>
    ///     What to call this app in a sentence. The game's name where there is one, and the bare app id
    ///     otherwise, so a message about an id nobody recognises still says which id.
    /// </summary>
    public static string Describe(uint appId)
    {
        return ForAppId(appId)?.HumanFriendlyGameName ?? $"Steam app {appId}";
    }
}
