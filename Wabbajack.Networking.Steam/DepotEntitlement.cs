using SteamKit2;

namespace Wabbajack.Networking.Steam;

/// <summary>
///     Whether an account may open a depot at all. Steam will simply refuse to hand over a decryption key
///     without a licence, so asking first is the difference between "you do not own this" and an opaque
///     failure three calls later.
///     Pure, so the decision can be tested without a Steam connection: the callers supply the
///     <see cref="KeyValue" /> trees PICS already returned.
/// </summary>
public static class DepotEntitlement
{
    /// <summary>
    ///     True when a package grants the given depot.
    ///     Both <c>appids</c> and <c>depotids</c> are checked against the <em>depot</em> id, which reads
    ///     oddly but is right: Steam numbers a game's first depot the same as its app, and packages list
    ///     whichever of the two the store entry happens to use. Valve's own tooling compares both the same
    ///     way.
    /// </summary>
    public static bool PackageGrantsDepot(KeyValue? packageInfo, uint depotId)
    {
        if (packageInfo == null) return false;

        return ListContains(packageInfo["appids"], depotId) || ListContains(packageInfo["depotids"], depotId);
    }

    /// <summary>
    ///     True when the app is free to download, which is how a free tool -- the Creation Kit is the case
    ///     this exists for -- is reachable by an account that holds no licence naming it.
    /// </summary>
    public static bool IsFreeToDownload(KeyValue? appInfo)
    {
        return appInfo != null && appInfo["common"]["FreeToDownload"].AsBoolean();
    }

    /// <summary>
    ///     Whether to ask Steam for the licence it gives away, given what the licence scan established.
    ///     <para>
    ///         The third case is the point of this existing. Asking adds a package to somebody's Steam
    ///         library, and "we could not find out what they own" is not "they own nothing" - a licence
    ///         list that has not arrived on a slow link would otherwise put the Creation Kit in the library
    ///         of a user who already had it. <see cref="SteamContentClient.CheckAccessAsync" />'s
    ///         <see cref="DepotAccess.Unconfirmed" /> refuses to guess for exactly this reason, and a guess
    ///         that ends in writing to the account is the one least worth making.
    ///     </para>
    /// </summary>
    public static FreeLicenseDecision DecideFreeLicense(bool held, bool licensesArrived)
    {
        if (held) return FreeLicenseDecision.AlreadyHeld;

        return licensesArrived ? FreeLicenseDecision.Ask : FreeLicenseDecision.Unconfirmed;
    }

    private static bool ListContains(KeyValue list, uint value)
    {
        return list != KeyValue.Invalid && list.Children.Any(child => child.AsUnsignedInteger() == value);
    }
}

/// <summary>What the licence scan means for asking Steam to grant a free app.</summary>
public enum FreeLicenseDecision
{
    /// <summary>A licence already names the app. Nothing to ask for.</summary>
    AlreadyHeld,

    /// <summary>The account's licences are known and none of them covers it. Ask.</summary>
    Ask,

    /// <summary>
    ///     The licence list never arrived, so whether the account already holds one is unknown. Do not ask:
    ///     an unknown is not a no, and the cost of being wrong is a package in somebody's library.
    /// </summary>
    Unconfirmed
}
