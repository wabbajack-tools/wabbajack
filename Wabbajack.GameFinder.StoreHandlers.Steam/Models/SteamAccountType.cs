using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Known account types for a Steam account.
/// </summary>
/// <remarks>
/// This data was sourced from https://developer.valvesoftware.com/wiki/SteamID.
/// </remarks>
[SuppressMessage("ReSharper", "CommentTypo")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[PublicAPI]
public enum SteamAccountType : byte
{
    /// <summary>
    /// Invalid. This can't be used and has the letter <c>I</c> or <c>i</c>.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Single user account. Has the letter <c>U</c>.
    /// </summary>
    Individual = 1,

    /// <summary>
    /// Multiseat (e.g. cybercafe) account. Has the letter <c>M</c>.
    /// </summary>
    Multiseat = 2,

    /// <summary>
    /// Game server account. Has the letter <c>G</c>.
    /// </summary>
    GameServer = 3,

    /// <summary>
    /// Anonymous game server account. Has the letter <c>A</c>.
    /// </summary>
    AnonGameServer = 4,

    /// <summary>
    /// A pending user account which credentials are not yet verified by Steam.
    /// This can't be used and has the letter <c>P</c>.
    /// </summary>
    Pending = 5,

    /// <summary>
    /// Content server. Usage is unknown and it has the letter <c>C</c>.
    /// </summary>
    ContentServer = 6,

    /// <summary>
    /// Group. Has the letter <c>g</c>.
    /// </summary>
    Clan = 7,

    /// <summary>
    /// Chat. Has the letters <c>T</c>, <c>L</c> or <c>c</c>.
    /// </summary>
    Chat = 8,

    /// <summary>
    /// Local PSN account on PS3 or Live account on 360. Can't be used and has no letter.
    /// </summary>
    P2PSuperSeeder = 9,

    /// <summary>
    /// Anonymous user. Has the letter <c>a</c>.
    /// </summary>
    AnonUser = 10,
}

/// <summary>
/// Extension methods for <see cref="SteamAccountType"/>.
/// </summary>
[PublicAPI]
public static class SteamAccountTypeExtensions
{
    /// <summary>
    /// Gets the letter assigned to the provided account type.
    /// </summary>
    /// <remarks>
    /// Returns <c>?</c> if the account type is unknown or doesn't have
    /// a letter associated with it.
    /// </remarks>
    /// <param name="accountType"></param>
    /// <returns></returns>
    public static char GetLetter(this SteamAccountType accountType) => accountType switch
    {
        SteamAccountType.Invalid => 'I',
        SteamAccountType.Individual => 'U',
        SteamAccountType.Multiseat => 'M',
        SteamAccountType.GameServer => 'G',
        SteamAccountType.AnonGameServer => 'A',
        SteamAccountType.Pending => 'P',
        SteamAccountType.ContentServer => 'C',
        SteamAccountType.Clan => 'g',
        SteamAccountType.Chat => 'T',
        SteamAccountType.AnonUser => 'a',
        _ => '?'
    };
}
