namespace Wabbajack.Networking.Steam;

/// <summary>
///     What the entitlement check was able to establish. Three answers rather than two, because "we asked
///     and the answer was no" and "we could not ask" lead somewhere completely different for the user, and
///     collapsing them tells an owner to go and buy a game they already own.
/// </summary>
public enum DepotAccess
{
    /// <summary>A licence names the depot, or the app is free to download.</summary>
    Granted,

    /// <summary>The licence list arrived and nothing in it covers the depot, nor is the app free.</summary>
    NotEntitled,

    /// <summary>
    ///     The licence list never arrived, so nothing is known either way. Not a denial.
    /// </summary>
    Unconfirmed
}
