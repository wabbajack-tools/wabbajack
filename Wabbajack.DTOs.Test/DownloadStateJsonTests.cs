using System.Collections.Generic;
using System.Text.Json;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Xunit;

namespace Wabbajack.DTOs.Test;

/// <summary>
///     The download states for sources the user now fetches in a browser have no downloader project
///     behind them any more, only the metadata-only stand-ins in Wabbajack.Downloaders.ManualSources.
///     Their JsonName and JsonAlias attributes are the only thing letting a published modlist - which
///     carries the type names the original Wabbajack.Lib classes wrote - still be read. Deleting one
///     would break reading every list that uses that source, silently and only for users with an old
///     list, so every accepted name gets a row here.
/// </summary>
public class DownloadStateJsonTests
{
    private readonly DTOSerializer _serializer;

    public DownloadStateJsonTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <summary>
    ///     Every $type string a modlist may carry for a browser-fetched source, with the state it must
    ///     deserialize into. The long names are what Wabbajack.Lib wrote; the short ones are the aliases
    ///     later versions accept.
    /// </summary>
    public static IEnumerable<object[]> LegacyTypeNames =>
        new List<object[]>
        {
            new object[] {"ManualDownloader, Wabbajack.Lib", typeof(Manual)},
            new object[] {"Manual", typeof(Manual)},
            new object[] {"MediaFireDownloader+State, Wabbajack.Lib", typeof(MediaFire)},
            new object[] {"MediaFireDownloader", typeof(MediaFire)},
            new object[] {"MediaFire", typeof(MediaFire)},
            new object[] {"MegaDownloader, Wabbajack.Lib", typeof(Mega)},
            new object[] {"Mega", typeof(Mega)},
            new object[] {"GoogleDriveDownloader, Wabbajack.Lib", typeof(GoogleDrive)},
            new object[] {"GoogleDrive", typeof(GoogleDrive)},
            new object[] {"ModDBDownloader, Wabbajack.Lib", typeof(ModDB)},
            new object[] {"ModDBDownloader", typeof(ModDB)},
            new object[] {"ModDB", typeof(ModDB)},
            new object[] {"LoversLabOAuthDownloader, Wabbajack.Lib", typeof(LoversLab)},
            new object[] {"LoversLab", typeof(LoversLab)},
            new object[] {"VectorPlexusOAuthDownloader+State, Wabbajack.Lib", typeof(VectorPlexus)},
            new object[] {"VectorPlexus", typeof(VectorPlexus)},
            new object[] {"Bethesda", typeof(Bethesda)}
        };

    [Theory]
    [MemberData(nameof(LegacyTypeNames))]
    public void EveryLegacyTypeNameStillDeserializes(string typeName, System.Type expected)
    {
        var json = $$"""
            {
              "$type": "Archive, Wabbajack.Lib",
              "Hash": "PfX6zen7m2M=",
              "Name": "WABBAJACK_TEST_FILE.zip",
              "Size": 1024,
              "State": {
                "$type": "{{typeName}}",
                "Url": "https://example.com/files/WABBAJACK_TEST_FILE.zip",
                "Id": "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_",
                "Prompt": "Click the green button",
                "IPS4Mod": 11116,
                "IPS4File": "WABBAJACK_TEST_FILE.zip",
                "Game": "SkyrimSpecialEdition",
                "IsCCMod": true,
                "ProductId": 4,
                "BranchId": 90898,
                "ContentId": "4059054"
              }
            }
            """;

        var archive = _serializer.Deserialize<Archive>(json);

        Assert.NotNull(archive);
        Assert.IsType(expected, archive!.State);
    }

    /// <summary>
    ///     Serializing a state writes its JsonName back, and reading that returns the same state, so a
    ///     list recompiled or mirrored through Wabbajack stays readable by every other version.
    /// </summary>
    public static IEnumerable<object[]> States =>
        new List<object[]>
        {
            new object[] {new Manual {Url = new System.Uri("https://example.com/a.zip"), Prompt = "Click"}},
            new object[] {new MediaFire {Url = new System.Uri("http://www.mediafire.com/file/a/a.zip")}},
            new object[] {new Mega {Url = new System.Uri("https://mega.nz/file/a#b")}},
            new object[] {new GoogleDrive {Id = "1grLRTrpHxlg7VPxATTFNfq2OkU_Plvh_"}},
            new object[] {new ModDB {Url = new System.Uri("https://www.moddb.com/downloads/start/1")}},
            new object[] {new LoversLab {IPS4Mod = 11116, IPS4File = "a.zip"}},
            new object[] {new LoversLab {IPS4Mod = 853295, IsAttachment = true}},
            new object[] {new VectorPlexus {IPS4Mod = 290, IPS4File = "a.zip"}},
            new object[]
            {
                new Bethesda
                {
                    Game = Game.SkyrimSpecialEdition, IsCCMod = true, ProductId = 4, BranchId = 90898,
                    ContentId = "4059054", Name = "ccbgssse001-fish"
                }
            }
        };

    [Theory]
    [MemberData(nameof(States))]
    public void StateRoundTripsThroughModlistJson(IDownloadState state)
    {
        var archive = new Archive {Name = "WABBAJACK_TEST_FILE.zip", Size = 1024, State = state};

        var json = _serializer.Serialize(archive);
        var read = _serializer.Deserialize<Archive>(json);

        Assert.NotNull(read);
        Assert.IsType(state.GetType(), read!.State);
        Assert.Equal(state.PrimaryKeyString, read.State.PrimaryKeyString);
        Assert.Equal(state.TypeName, read.State.TypeName);
        Assert.Equal(json, _serializer.Serialize(read));
    }

    /// <summary>
    ///     The $type a state writes has to be one the reader accepts. A JsonName renamed without a
    ///     matching alias would pass the round-trip above and still be unreadable by older builds, so
    ///     the written name is checked against the table of accepted names directly.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void SerializedTypeNameIsOneTheReaderAccepts(IDownloadState state)
    {
        var json = _serializer.Serialize(state);
        using var doc = JsonDocument.Parse(json);
        var written = doc.RootElement.GetProperty("$type").GetString();

        Assert.Contains(LegacyTypeNames, row => (string) row[0] == written && (System.Type) row[1] == state.GetType());
    }
}
