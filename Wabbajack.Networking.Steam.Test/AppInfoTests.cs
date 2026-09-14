using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.Steam.DTOs;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Reading Steam's <c>depots</c> section, which is the one thing here that has already broken on real
///     PICS data: it is not a dictionary of depots, whatever the name says, and typing it as one made
///     reading any real app throw.
///     Every value in these fixtures is a JSON string, because that is what
///     <see cref="KeyValueTranslator" /> emits -- Steam's KeyValues have no notion of a number -- so
///     anything read back as one depends on the serializer's AllowReadingFromString.
/// </summary>
public class AppInfoTests
{
    private static readonly JsonSerializerOptions Options =
        new DTOSerializer(Array.Empty<JsonConverter>()).Options;

    /// <summary>The shape a real app's depots section actually has, keys and all.</summary>
    private const string RealShape = """
        {
          "depots": {
            "489830": {
              "name": "Skyrim Special Edition",
              "config": {"oslist": "windows"},
              "maxsize": "6760136813",
              "manifests": {"public": {"gid": "3660787314279169352", "size": "1", "download": "2"}}
            },
            "489831": {
              "name": "Older style depot",
              "manifests": {"public": "2756691988703496654"}
            },
            "branches": {"public": {"buildid": "10584036", "timeupdated": "1672860000"}},
            "baselanguages": "english",
            "hasdepotsindlc": "0",
            "overridescddb": "1"
          }
        }
        """;

    [Fact]
    public void TheSectionIsReadableAtAllDespiteItsMixedContents()
    {
        // The regression this exists for: baselanguages is a bare string sitting among the depots, and
        // deserializing the section as depots threw on it.
        var app = Parse(RealShape);

        Assert.Equal(2, app.GetDepots(Options).Count());
    }

    [Fact]
    public void OnlyNumberedEntriesAreDepots()
    {
        var app = Parse(RealShape);

        Assert.Equal(new uint[] {489830, 489831},
            app.GetDepots(Options).Select(d => d.DepotId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void ADepotsFieldsSurviveBeingWrittenAsStrings()
    {
        var depot = Depot(RealShape, 489830);

        Assert.Equal("Skyrim Special Edition", depot.Name);
        Assert.Equal(6760136813ul, depot.MaxSize);
        Assert.Equal("windows", depot.Config.OSList);
    }

    [Fact]
    public void AManifestWrittenAsAnObjectIsReadFromItsGid()
    {
        Assert.Equal(3660787314279169352ul, Depot(RealShape, 489830).ManifestFor("public"));
    }

    [Fact]
    public void AManifestWrittenAsABareStringIsReadDirectly()
    {
        // Both forms are still in the wild, and assuming either one is how this breaks again.
        Assert.Equal(2756691988703496654ul, Depot(RealShape, 489831).ManifestFor("public"));
    }

    [Fact]
    public void ABranchWithNoManifestIsNullRatherThanZero()
    {
        // Zero is a manifest id Steam could in principle hand back, so "absent" must not look like it.
        Assert.Null(Depot(RealShape, 489830).ManifestFor("beta"));
    }

    [Theory]
    // Steam is not the only thing that writes this file, and a shape nobody expected must not throw.
    [InlineData("""{"depots": {"1": {"manifests": {"public": {"size": "1"}}}}}""")]
    [InlineData("""{"depots": {"1": {"manifests": {"public": {"gid": "not a number"}}}}}""")]
    [InlineData("""{"depots": {"1": {"manifests": {"public": "not a number"}}}}""")]
    [InlineData("""{"depots": {"1": {"manifests": {"public": ["an", "array"]}}}}""")]
    [InlineData("""{"depots": {"1": {}}}""")]
    public void AManifestThatCannotBeReadIsNullRatherThanAThrow(string json)
    {
        Assert.Null(Depot(json, 1).ManifestFor("public"));
    }

    [Theory]
    [InlineData("""{"depots": {"branches": {"public": {"buildid": "1"}}}}""")]
    [InlineData("""{"depots": {"baselanguages": "english"}}""")]
    [InlineData("""{"depots": {"12345678901234567890": {"name": "past uint"}}}""")]
    [InlineData("""{"depots": {"489830": "a depot written as a string"}}""")]
    [InlineData("""{"depots": {}}""")]
    [InlineData("{}")]
    public void AnythingThatIsNotANumberedObjectIsSkippedRatherThanGuessedAt(string json)
    {
        Assert.Empty(Parse(json).GetDepots(Options));
    }

    private static AppInfo Parse(string json)
    {
        return JsonSerializer.Deserialize<AppInfo>(json, Options)!;
    }

    private static Depot Depot(string json, uint depotId)
    {
        return Parse(json).GetDepots(Options).Single(d => d.DepotId == depotId).Depot;
    }
}
