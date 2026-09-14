using System;
using System.Collections.Generic;
using System.Linq;
using SteamKit2;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Whether an account may open a depot. Getting this wrong in the permissive direction only costs a
///     clearer error; getting it wrong in the strict direction tells someone who owns a game that they do
///     not, which is the failure worth pinning.
/// </summary>
public class DepotEntitlementTests
{
    [Fact]
    public void ADepotListedUnderDepotidsIsGranted()
    {
        var package = Package(depotIds: new uint[] {489830, 489831, 489832});

        Assert.True(DepotEntitlement.PackageGrantsDepot(package, 489831));
    }

    [Fact]
    public void ADepotListedUnderAppidsIsGranted()
    {
        // Steam numbers a game's first depot the same as its app, and packages list whichever of the two
        // the store entry happened to use, so both lists are compared against the depot id.
        var package = Package(new uint[] {489830});

        Assert.True(DepotEntitlement.PackageGrantsDepot(package, 489830));
    }

    [Fact]
    public void ADepotInNeitherListIsNotGranted()
    {
        var package = Package(new uint[] {440}, new uint[] {441, 442});

        Assert.False(DepotEntitlement.PackageGrantsDepot(package, 489831));
    }

    [Fact]
    public void APackageSteamSaidNothingAboutGrantsNothing()
    {
        // PICS returns null for a package it does not know, and a null must not read as a yes.
        Assert.False(DepotEntitlement.PackageGrantsDepot(null, 489831));
    }

    [Fact]
    public void AnEmptyPackageGrantsNothing()
    {
        Assert.False(DepotEntitlement.PackageGrantsDepot(new KeyValue("package"), 489831));
    }

    [Fact]
    public void FreeToDownloadIsHowAFreeToolIsReachedWithoutALicence()
    {
        var app = new KeyValue("app");
        var common = new KeyValue("common");
        common.Children.Add(new KeyValue("FreeToDownload", "1"));
        app.Children.Add(common);

        Assert.True(DepotEntitlement.IsFreeToDownload(app));
    }

    [Fact]
    public void AnAppThatIsNotFreeSaysSo()
    {
        var app = new KeyValue("app");
        var common = new KeyValue("common");
        common.Children.Add(new KeyValue("FreeToDownload", "0"));
        app.Children.Add(common);

        Assert.False(DepotEntitlement.IsFreeToDownload(app));
    }

    [Fact]
    public void AnAppWithoutTheKeyIsNotFree()
    {
        Assert.False(DepotEntitlement.IsFreeToDownload(new KeyValue("app")));
        Assert.False(DepotEntitlement.IsFreeToDownload(null));
    }

    private static KeyValue Package(uint[]? appIds = null, uint[]? depotIds = null)
    {
        var package = new KeyValue("package");

        if (appIds != null) package.Children.Add(List("appids", appIds));
        if (depotIds != null) package.Children.Add(List("depotids", depotIds));

        return package;
    }

    private static KeyValue List(string name, uint[] values)
    {
        var list = new KeyValue(name);
        var index = 0;
        foreach (var value in values) list.Children.Add(new KeyValue((index++).ToString(), value.ToString()));
        return list;
    }
}
