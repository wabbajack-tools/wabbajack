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

    private static bool ListContains(KeyValue list, uint value)
    {
        return list != KeyValue.Invalid && list.Children.Any(child => child.AsUnsignedInteger() == value);
    }
}
