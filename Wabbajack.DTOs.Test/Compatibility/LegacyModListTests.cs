using System;
using System.Linq;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Lists published years ago are still installed today, and their <c>modlist</c> entry is read by
///     the current DTOs with no migration step. These snippets are cut down from real lists (the first
///     from Halgari's Helper, compiled by Wabbajack 2.2.2) so a change that stops an old list loading,
///     or loads it with different values, fails here rather than on a user's machine.
/// </summary>
public class LegacyModListTests
{
    private readonly DTOSerializer _serializer;

    public LegacyModListTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <summary>
    ///     Cut from the <c>modlist</c> entry of halgaris_helper.wabbajack, keeping its shapes and values:
    ///     a 2.x Http state, FromArchive, InlineFile and RemappedInlineFile directives, and the top-level
    ///     fields as that version wrote them (no OtherGames, no Community).
    /// </summary>
    private const string HalgarisHelper = """
        {"Archives":[{"Hash":"LqKnmSUrXac=","Meta":"[General]\ndirectURL=https://github.com/ModOrganizer2/modorganizer/releases/download/v2.4.4/Mod.Organizer-2.4.4.7z","Name":"Mod.Organizer-2.4.4.7z","Size":90051560,"State":{"$type":"HttpDownloader, Wabbajack.Lib","Headers":[],"Url":"https://github.com/ModOrganizer2/modorganizer/releases/download/v2.4.4/Mod.Organizer-2.4.4.7z"}}],"Author":"halgari","Description":"","Directives":[{"$type":"FromArchive","ArchiveHashPath":["LqKnmSUrXac=","boost_python38-vc142-mt-x64-1_75.dll"],"Hash":"IfmLVDqJxVk=","Size":191488,"To":"boost_python38-vc142-mt-x64-1_75.dll"},{"$type":"FromArchive","ArchiveHashPath":["LqKnmSUrXac=","stylesheets\\Night Eyes.qss"],"Hash":"/brB4w0cf7I=","Size":11286,"To":"stylesheets\\Night Eyes.qss"},{"$type":"InlineFile","Hash":"hGwfMvkWrkI=","Size":49,"SourceDataID":"0077686f-a6ee-4154-891f-f012a892149c","To":"dump_running_process.bat"},{"$type":"InlineFile","Hash":"menYUTfbRu8=","Size":0,"SourceDataID":"b4d5b522-3760-45b9-8d92-013a4f8b304c","To":"profiles\\Default\\archives.txt"},{"$type":"RemappedInlineFile","Hash":"EWQy/K9DKJQ=","Size":662,"SourceDataID":"6edb3935-0436-4c43-956b-c61381a7996e","To":"Default.compiler_settings"},{"$type":"FromArchive","ArchiveHashPath":["LqKnmSUrXac=","stylesheets\\Paper\\Mono\\White\\Arrows\\Selected\\down.svg"],"Hash":"aXkQLuPLXZo=","Size":3188,"To":"stylesheets\\Paper\\Mono\\White\\Arrows\\Selected\\down.svg"}],"GameType":"SkyrimSpecialEdition","Image":"modlist-image.png","Name":"HalgarisHelper","Readme":"","WabbajackVersion":"2.2.2.0","Website":"http://www.wabbajack.org/","Version":"0.0.1.0","IsNSFW":false}
        """;

    [Fact]
    public void HalgarisHelperLoadsWithTheValuesItWasWrittenWith()
    {
        var list = _serializer.Deserialize<ModList>(HalgarisHelper)!;

        Assert.Equal("HalgarisHelper", list.Name);
        Assert.Equal("halgari", list.Author);
        Assert.Equal("", list.Description);
        Assert.Equal(Game.SkyrimSpecialEdition, list.GameType);
        Assert.Empty(list.OtherGames);
        Assert.Equal("", list.Community);
        Assert.Equal("modlist-image.png", list.Image.ToString());
        Assert.Equal("", list.Readme);
        Assert.Equal(new Version(2, 2, 2, 0), list.WabbajackVersion);
        Assert.Equal("http://www.wabbajack.org/", list.Website);
        Assert.Equal(new Version(0, 0, 1, 0), list.Version);
        Assert.False(list.IsNSFW);

        var archive = Assert.Single(list.Archives);
        Assert.Equal(Hash.FromULong(0xA75D2B2599A7A22E), archive.Hash);
        Assert.Equal("Mod.Organizer-2.4.4.7z", archive.Name);
        Assert.Equal(90051560, archive.Size);
        Assert.Equal(
            "[General]\ndirectURL=https://github.com/ModOrganizer2/modorganizer/releases/download/v2.4.4/Mod.Organizer-2.4.4.7z",
            archive.Meta);
        var http = Assert.IsType<Http>(archive.State);
        Assert.Equal(
            new Uri("https://github.com/ModOrganizer2/modorganizer/releases/download/v2.4.4/Mod.Organizer-2.4.4.7z"),
            http.Url);
        Assert.Empty(http.Headers);

        Assert.Equal(
            new[]
            {
                typeof(FromArchive), typeof(FromArchive), typeof(InlineFile), typeof(InlineFile),
                typeof(RemappedInlineFile), typeof(FromArchive)
            },
            list.Directives.Select(d => d.GetType()));

        var fromArchive = (FromArchive) list.Directives[1];
        Assert.Equal(archive.Hash, fromArchive.ArchiveHashPath.Hash);
        Assert.Equal(new[] {@"stylesheets\Night Eyes.qss"},
            fromArchive.ArchiveHashPath.Parts.Select(p => p.ToString()));
        Assert.Equal(Hash.FromULong(0xB27F1C0DE3C1BAFD), fromArchive.Hash);
        Assert.Equal(11286, fromArchive.Size);
        Assert.Equal(@"stylesheets\Night Eyes.qss", fromArchive.To.ToString());

        var inline = (InlineFile) list.Directives[3];
        Assert.Equal("b4d5b522-3760-45b9-8d92-013a4f8b304c", inline.SourceDataID.ToString());
        Assert.Equal(@"profiles\Default\archives.txt", inline.To.ToString());
        Assert.Equal(0, inline.Size);

        var remapped = (RemappedInlineFile) list.Directives[4];
        Assert.Equal(Hash.FromULong(0x942843AFFC326411), remapped.Hash);
        Assert.Equal("6edb3935-0436-4c43-956b-c61381a7996e", remapped.SourceDataID.ToString());
        Assert.Equal("Default.compiler_settings", remapped.To.ToString());
    }

    /// <summary>
    ///     Loaded, written by today's code and loaded again, the list is the same; and writing it a
    ///     second time gives the same text, so recompiling or mirroring a list does not drift.
    /// </summary>
    [Fact]
    public void HalgarisHelperSurvivesBeingWrittenAgain()
    {
        var first = _serializer.Deserialize<ModList>(HalgarisHelper)!;
        var json = _serializer.Serialize(first);
        var second = _serializer.Deserialize<ModList>(json)!;

        DeepCompare.AssertEqual(first, second, "ModList");
        Assert.Equal(json, _serializer.Serialize(second));
    }

    /// <summary>
    ///     A 2.x-era list using the long "..., Wabbajack.Lib" and "..., Compression.BSA" names for every
    ///     family, including a BSA rebuilt from its files and a patched file. Property names are the
    ///     ones Wabbajack.Lib wrote.
    /// </summary>
    private const string LegacyNamesEverywhere = """
        {
          "Archives": [
            {
              "Hash": "782riWdFIwE=",
              "Name": "SkyUI_5_2_SE-12604-5-2SE.7z",
              "Size": 2716857,
              "Meta": "[General]\ngameName=skyrimspecialedition\nmodID=12604\nfileID=35407",
              "State": {
                "$type": "NexusDownloader, Wabbajack.Lib",
                "Author": "SkyUI Team",
                "Description": "Elegant, PC-friendly interface mod",
                "FileID": 35407,
                "GameName": "SkyrimSpecialEdition",
                "ImageURL": "https://staticdelivery.nexusmods.com/mods/1704/images/12604/12604-1.png",
                "IsNSFW": false,
                "ModID": 12604,
                "Name": "SkyUI",
                "Version": "5.2SE"
              }
            },
            {
              "Hash": "//////////8=",
              "Name": "Skyrim - Textures0.bsa",
              "Size": 643695305,
              "State": {
                "$type": "GameFileSourceDownloader, Wabbajack.Lib",
                "Game": "SkyrimSpecialEdition",
                "GameFile": "Data\\Skyrim - Textures0.bsa",
                "GameVersion": "1.5.97.0",
                "Hash": "//////////8="
              }
            }
          ],
          "Author": "Someone",
          "Description": "Uses every legacy name",
          "Directives": [
            {
              "$type": "PatchedFromArchive, Wabbajack.Lib",
              "ArchiveHashPath": ["782riWdFIwE=", "Interface\\skyui\\config.txt"],
              "FromHash": "LqKnmSUrXac=",
              "Hash": "IfmLVDqJxVk=",
              "PatchID": "8a1f2f0e-0000-4000-8000-000000000001",
              "Size": 4096,
              "To": "mods\\SkyUI\\Interface\\skyui\\config.txt"
            },
            {
              "$type": "CreateBSA, Wabbajack.Lib",
              "Hash": "treZwcvvkTE=",
              "Size": 123456,
              "TempID": "8a1f2f0e-0000-4000-8000-000000000002",
              "To": "mods\\SkyUI\\SkyUI_SE.bsa",
              "State": {
                "$type": "BSAState, Compression.BSA",
                "ArchiveFlags": 1795,
                "FileFlags": 2,
                "Magic": "BSA\u0000",
                "Version": 105
              },
              "FileStates": [
                {"$type": "BSAFileState, Compression.BSA", "FlipCompression": false, "Index": 0, "Path": "interface\\skyui\\icons.swf"},
                {"$type": "BSAFileState, Compression.BSA", "FlipCompression": true, "Index": 1, "Path": "interface\\skyui\\map.swf"}
              ]
            },
            {
              "$type": "MergedPatch, Wabbajack.Lib",
              "Hash": "kW5NW5FEqyE=",
              "PatchID": "8a1f2f0e-0000-4000-8000-000000000003",
              "Size": 2048,
              "Sources": [{"Hash": "WV7iGczoFPo=", "RelativePath": "mods\\A\\a.esp"}],
              "To": "mods\\Merged\\merged.esp"
            },
            {
              "$type": "InlineFile, Wabbajack.Lib",
              "Hash": "hGwfMvkWrkI=",
              "Size": 49,
              "SourceDataID": "0077686f-a6ee-4154-891f-f012a892149c",
              "To": "ModOrganizer.ini"
            }
          ],
          "GameType": "SkyrimSpecialEdition",
          "Name": "Legacy names",
          "Version": "1.2.3.4",
          "WabbajackVersion": "2.4.1.1",
          "IsNSFW": true
        }
        """;

    [Fact]
    public void LegacyNamesInEveryFamilyLoad()
    {
        var list = _serializer.Deserialize<ModList>(LegacyNamesEverywhere)!;

        var nexus = Assert.IsType<Nexus>(list.Archives[0].State);
        Assert.Equal(Game.SkyrimSpecialEdition, nexus.Game);
        Assert.Equal(12604, nexus.ModID);
        Assert.Equal(35407, nexus.FileID);
        Assert.Equal("SkyUI", nexus.Name);
        Assert.Equal("SkyUI Team", nexus.Author);
        Assert.Equal("5.2SE", nexus.Version);
        Assert.Equal(new Uri("https://staticdelivery.nexusmods.com/mods/1704/images/12604/12604-1.png"),
            nexus.ImageURL);
        Assert.Equal("NexusDownloader+State|SkyrimSpecialEdition|12604|35407", nexus.PrimaryKeyString);

        var gameFile = Assert.IsType<GameFileSource>(list.Archives[1].State);
        Assert.Equal(Game.SkyrimSpecialEdition, gameFile.Game);
        Assert.Equal(@"Data\Skyrim - Textures0.bsa", gameFile.GameFile.ToString());
        Assert.Equal("1.5.97.0", gameFile.GameVersion);
        Assert.Equal(Hash.FromULong(ulong.MaxValue), gameFile.Hash);

        var patched = Assert.IsType<PatchedFromArchive>(list.Directives[0]);
        Assert.Equal(Hash.FromULong(0x0123456789ABCDEF), patched.ArchiveHashPath.Hash);
        Assert.Equal(Hash.FromULong(0xA75D2B2599A7A22E), patched.FromHash);
        Assert.Equal("8a1f2f0e-0000-4000-8000-000000000001", patched.PatchID.ToString());

        var bsa = Assert.IsType<CreateBSA>(list.Directives[1]);
        var bsaState = Assert.IsType<BSAState>(bsa.State);
        Assert.Equal("BSA\0", bsaState.Magic);
        Assert.Equal(105u, bsaState.Version);
        Assert.Equal(1795u, bsaState.ArchiveFlags);
        Assert.Equal(2u, bsaState.FileFlags);
        Assert.Equal(2, bsa.FileStates.Length);
        var file = Assert.IsType<BSAFile>(bsa.FileStates[1]);
        Assert.True(file.FlipCompression);
        Assert.Equal(1, file.Index);
        Assert.Equal(@"interface\skyui\map.swf", file.Path.ToString());

        var merged = Assert.IsType<MergedPatch>(list.Directives[2]);
        var source = Assert.Single(merged.Sources);
        Assert.Equal(@"mods\A\a.esp", source.RelativePath.ToString());

        Assert.IsType<InlineFile>(list.Directives[3]);

        Assert.Equal(new Version(1, 2, 3, 4), list.Version);
        Assert.Equal(new Version(2, 4, 1, 1), list.WabbajackVersion);
        Assert.True(list.IsNSFW);
    }

    /// <summary>
    ///     Read with legacy names and written again, the list comes back with the current names and the
    ///     same content.
    /// </summary>
    [Fact]
    public void LegacyNamesSurviveBeingWrittenAgain()
    {
        var first = _serializer.Deserialize<ModList>(LegacyNamesEverywhere)!;
        var json = _serializer.Serialize(first);
        var second = _serializer.Deserialize<ModList>(json)!;

        DeepCompare.AssertEqual(first, second, "ModList");
        Assert.Contains("\"$type\":\"PatchedFromArchive\"", json);
        Assert.Contains("\"$type\":\"BSAState, Compression.BSA\"", json);
    }
}
