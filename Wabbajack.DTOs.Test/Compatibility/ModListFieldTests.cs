using System;
using System.Collections.Generic;
using System.Linq;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     The top-level fields of a modlist, and of its entry in modlists.json, are read by every version of
///     the app and the gallery. Game names in particular arrive in more than one spelling: the modlist
///     carries the enum name, and Client.PublishModlist lowercases the "game" values it writes to
///     modlists.json. These tests pin both spellings and every other top-level field.
/// </summary>
public class ModListFieldTests
{
    private readonly DTOSerializer _serializer;

    public ModListFieldTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    public static IEnumerable<object[]> Games => Enum.GetValues<Game>().Select(g => new object[] {g});

    [Theory]
    [MemberData(nameof(Games))]
    public void GameTypeReadsInAnyCase(Game game)
    {
        foreach (var spelling in new[] {game.ToString(), game.ToString().ToLowerInvariant(), game.ToString().ToUpperInvariant()})
        {
            var list = _serializer.Deserialize<ModList>($$"""{"GameType": "{{spelling}}", "OtherGames": ["{{spelling}}"]}""")!;
            Assert.Equal(game, list.GameType);
            Assert.Equal(new[] {game}, list.OtherGames);
        }
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void GameTypeIsWrittenAsTheEnumName(Game game)
    {
        var json = _serializer.Serialize(new ModList {GameType = game, OtherGames = new[] {game}});

        Assert.Contains($"\"GameType\":\"{game}\"", json);
        Assert.Contains($"\"OtherGames\":[\"{game}\"]", json);
    }

    /// <summary>
    ///     An unknown game name fails the load. That is what an older app does with a list for a game
    ///     added after it was released.
    /// </summary>
    [Fact]
    public void AnUnknownGameThrows()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            _serializer.Deserialize<ModList>("""{"GameType": "SomeFutureGame"}"""));
    }

    /// <summary>
    ///     Does the same text replacement Client.PublishModlist does to modlists.json, and checks it
    ///     still finds the game, still lowercases it, and that the result still reads back. The
    ///     replacement depends on the indented writer putting exactly one space after the colon and
    ///     "game" not being the last property, which this would catch changing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Games))]
    public void PublishedGameNamesAreLowercasedAndStillRead(Game game)
    {
        var metadata = new[]
        {
            new ModlistMetadata
            {
                Title = "A list", Game = game, Links = new LinksObject {MachineURL = "a_list"},
                Version = new Version(1, 2, 3, 4)
            }
        };

        var json = _serializer.Serialize(metadata, true);
        var published = GameRegistry.Games.Keys.Aggregate(json,
            (current, g) => current.Replace($"\"game\": \"{g}\",", $"\"game\": \"{g.ToString().ToLower()}\","));

        if (GameRegistry.Games.ContainsKey(game))
            Assert.Contains($"\"game\": \"{game.ToString().ToLower()}\",", published);

        var read = _serializer.Deserialize<ModlistMetadata[]>(published)!;
        Assert.Equal(game, Assert.Single(read).Game);
        Assert.Equal(new Version(1, 2, 3, 4), read[0].Version);
        Assert.Equal("a_list", read[0].Links.MachineURL);
    }

    [Fact]
    public void ModlistsJsonEntryReadsLowercaseGame()
    {
        const string json = """
            [{
              "title": "Halgari's Helper",
              "description": "A list for testing",
              "author": "halgari",
              "maintainers": ["halgari"],
              "game": "skyrimspecialedition",
              "official": false,
              "tags": ["Testing"],
              "nsfw": true,
              "utility_list": true,
              "image_contains_title": false,
              "force_down": false,
              "links": {"image": "https://example.com/i.png", "readme": "https://example.com/r", "download": "https://example.com/d.wabbajack", "machineURL": "halgaris_helper"},
              "version": "0.0.1.0",
              "dateCreated": "2021-01-02T03:04:05Z",
              "dateUpdated": "2023-10-23T00:00:00Z",
              "repositoryName": "wj-featured",
              "added_by_a_newer_gallery": {"anything": 1}
            }]
            """;

        var entry = Assert.Single(_serializer.Deserialize<ModlistMetadata[]>(json)!);
        Assert.Equal(Game.SkyrimSpecialEdition, entry.Game);
        Assert.Equal("Halgari's Helper", entry.Title);
        Assert.Equal(new[] {"halgari"}, entry.Maintainers);
        Assert.True(entry.NSFW);
        Assert.True(entry.UtilityList);
        Assert.Equal("halgaris_helper", entry.Links.MachineURL);
        Assert.Equal(new Version(0, 0, 1, 0), entry.Version);
        Assert.Equal("wj-featured/halgaris_helper", entry.NamespacedName);
    }

    /// <summary>
    ///     Every top-level field of a ModList, filled with non-default values, comes back as it went in.
    ///     Uses the same builder as the polymorphic tests; the archive list gets one of each download
    ///     state and the directive list one of each directive.
    /// </summary>
    [Fact]
    public void EveryTopLevelFieldRoundTrips()
    {
        var builder = new SampleBuilder();
        var list = (ModList) builder.Build(typeof(ModList));
        list.Archives = ModlistTypes.ConcreteTypes(typeof(IDownloadState))
            .Select(t =>
            {
                var archive = (Archive) builder.Build(typeof(Archive));
                archive.State = (IDownloadState) builder.Build(t);
                return archive;
            })
            .ToArray();
        Assert.Equal(ModlistTypes.ConcreteTypes(typeof(Directive)).Length, list.Directives.Length);

        var json = _serializer.Serialize(list);
        var read = _serializer.Deserialize<ModList>(json)!;

        DeepCompare.AssertEqual(list, read, "ModList");
    }

    /// <summary>
    ///     The names of the top-level fields as written. Renaming one makes every older app read it as
    ///     missing.
    /// </summary>
    [Fact]
    public void TopLevelFieldNamesAreUnchanged()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(_serializer.Serialize(new ModList()));
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "Archives", "Author", "Community", "Description", "Directives", "GameType", "Image", "IsNSFW",
                "Name", "OtherGames", "Readme", "Version", "WabbajackVersion", "Website"
            },
            names);
    }

    [Fact]
    public void MissingTopLevelFieldsReadAsDefaults()
    {
        var list = _serializer.Deserialize<ModList>("{}")!;

        Assert.Empty(list.Archives);
        Assert.Empty(list.Directives);
        Assert.Empty(list.OtherGames);
        Assert.Equal("", list.Author);
        Assert.Equal("", list.Community);
        Assert.Equal(new Version(1, 0, 0, 0), list.Version);
        Assert.Null(list.WabbajackVersion);
        Assert.False(list.IsNSFW);
        Assert.Equal(default, list.GameType);
    }

    [Fact]
    public void NullWabbajackVersionRoundTrips()
    {
        var json = _serializer.Serialize(new ModList {WabbajackVersion = null});
        Assert.Contains("\"WabbajackVersion\":null", json);
        Assert.Null(_serializer.Deserialize<ModList>(json)!.WabbajackVersion);
    }

    [Theory]
    [InlineData("1.0", 1, 0, -1, -1)]
    [InlineData("1.2.3", 1, 2, 3, -1)]
    [InlineData("4.0.0.0", 4, 0, 0, 0)]
    [InlineData("0.0.1.0", 0, 0, 1, 0)]
    public void VersionsReadInEveryLength(string text, int major, int minor, int build, int revision)
    {
        var list = _serializer.Deserialize<ModList>($$"""{"Version": "{{text}}"}""")!;

        Assert.Equal(major, list.Version.Major);
        Assert.Equal(minor, list.Version.Minor);
        Assert.Equal(build, list.Version.Build);
        Assert.Equal(revision, list.Version.Revision);
        Assert.Contains($"\"Version\":\"{text}\"", _serializer.Serialize(list));
    }
}
