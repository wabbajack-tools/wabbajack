using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Universes available for Steam Accounts.
/// </summary>
/// <remarks>
/// The values for these enums were sourced from https://partner.steamgames.com/doc/api/steam_api#EUniverse
/// and https://developer.valvesoftware.com/wiki/SteamID#Universes_Available_for_Steam_Accounts.
/// </remarks>
[PublicAPI]
public enum SteamUniverse : uint
{
    /// <summary>
    /// Invalid.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// The standard public universe.
    /// </summary>
    Public = 1,

    /// <summary>
    /// Beta universe used inside Valve.
    /// </summary>
    Beta = 2,

    /// <summary>
    /// Internal universe used inside Valve.
    /// </summary>
    Internal = 3,

    /// <summary>
    /// Dev universe used inside Valve.
    /// </summary>
    Dev = 4,
}
