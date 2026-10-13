using System;
using System.Linq;
using System.Text.Json.Serialization;
using SteamKit2;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Steam.DTOs;
using Xunit;

namespace Wabbajack.Networking.Steam.Test.Offline;

/// <summary>
///     The whole path PICS product info takes on its way to a depot list: a SteamKit <see cref="KeyValue" />
///     tree, through <see cref="KeyValueTranslator" />, into <see cref="AppInfo" />. <c>AppInfoTests</c>
///     starts from JSON; this starts from the tree Steam actually hands back, so a change to how the
///     translator writes nested sections or scalars shows up here before a user's repair finds no depots.
/// </summary>
public class KeyValueTranslatorTests
{
    private static readonly DTOSerializer Dtos = new(Array.Empty<JsonConverter>());

    private static KeyValue Node(string name, params KeyValue[] children)
    {
        var node = new KeyValue(name);
        node.Children.AddRange(children);
        return node;
    }

    private static KeyValue Leaf(string name, string value)
    {
        return new KeyValue(name, value);
    }

    /// <summary>
    ///     Shaped like Skyrim Special Edition's real product info: the app root, a common section the
    ///     translator must not trip on, and a depots section mixing numbered depots with branches and bare
    ///     strings.
    /// </summary>
    private static KeyValue SkyrimSpecialEdition()
    {
        return Node("489830",
            Leaf("appid", "489830"),
            Node("common",
                Leaf("name", "The Elder Scrolls V: Skyrim Special Edition"),
                Leaf("type", "Game")),
            Node("depots",
                Node("489831",
                    Leaf("name", "Skyrim Special Edition Data"),
                    Node("config", Leaf("oslist", "windows"), Leaf("language", "")),
                    Leaf("maxsize", "15338563235"),
                    Node("manifests",
                        Node("public",
                            Leaf("gid", "3660787314279169352"),
                            Leaf("size", "15338563235"),
                            Leaf("download", "11542736672")))),
                Node("489833",
                    Leaf("name", "Skyrim Special Edition German"),
                    Node("config", Leaf("language", "german")),
                    Node("manifests", Leaf("public", "2756691988703496654"))),
                Node("1946182",
                    Leaf("depotfromapp", "1946180"),
                    Leaf("sharedinstall", "1")),
                Node("branches",
                    Node("public", Leaf("buildid", "13959924"), Leaf("timeupdated", "1712670220")),
                    Node("beta", Leaf("buildid", "13959000"), Leaf("pwdrequired", "1"))),
                Leaf("baselanguages", "english"),
                Leaf("hasdepotsindlc", "1")));
    }

    private static AppInfo Translate()
    {
        return KeyValueTranslator.Translate<AppInfo>(SkyrimSpecialEdition(), Dtos);
    }

    [Fact]
    public void OnlyTheNumberedDepotsComeOutAsDepots()
    {
        var depots = Translate().GetDepots(Dtos.Options).Select(d => d.DepotId).OrderBy(id => id);

        Assert.Equal(new uint[] {489831, 489833, 1946182}, depots);
    }

    [Fact]
    public void ADepotsScalarsSurviveBeingWrittenAsStrings()
    {
        var depot = Translate().GetDepots(Dtos.Options).Single(d => d.DepotId == 489831).Depot;

        Assert.Equal("Skyrim Special Edition Data", depot.Name);
        Assert.Equal(15338563235ul, depot.MaxSize);
        Assert.Equal("windows", depot.Config.OSList);
        Assert.Equal("", depot.Config.Language);
    }

    /// <summary>The current form: <c>manifests/public</c> is an object whose <c>gid</c> is the manifest id.</summary>
    [Fact]
    public void AManifestNestedUnderGidIsRead()
    {
        var depot = Translate().GetDepots(Dtos.Options).Single(d => d.DepotId == 489831).Depot;

        Assert.Equal(3660787314279169352ul, depot.ManifestFor("public"));
    }

    /// <summary>The older form, still in the wild: <c>manifests/public</c> is the id itself.</summary>
    [Fact]
    public void AManifestWrittenAsAScalarIsRead()
    {
        var depot = Translate().GetDepots(Dtos.Options).Single(d => d.DepotId == 489833).Depot;

        Assert.Equal(2756691988703496654ul, depot.ManifestFor("public"));
        Assert.Null(depot.ManifestFor("beta"));
    }

    /// <summary>A depot borrowed from another app keeps saying where it comes from, as a number.</summary>
    [Fact]
    public void ADepotFromAnotherAppKeepsItsSourceApp()
    {
        var depot = Translate().GetDepots(Dtos.Options).Single(d => d.DepotId == 1946182).Depot;

        Assert.Equal(1946180u, depot.DepotFromApp);
        Assert.Equal(1u, depot.SharedInstall);
        Assert.Null(depot.ManifestFor("public"));
    }

    /// <summary>A section with no children is written as an empty object rather than dropped or nulled.</summary>
    [Fact]
    public void AnEmptySectionIsAnEmptyObject()
    {
        var app = KeyValueTranslator.Translate<AppInfo>(Node("1", Node("depots")), Dtos);

        Assert.Empty(app.DepotSection);
        Assert.Empty(app.GetDepots(Dtos.Options));
    }
}
