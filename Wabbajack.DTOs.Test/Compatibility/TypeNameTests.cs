using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     A modlist can carry any <c>$type</c> name some release of Wabbajack has written, back to the
///     "HttpDownloader, Wabbajack.Lib" style of 2.x. Each name is accepted only because a JsonName or
///     JsonAlias attribute says so and Generated.cs was regenerated after it was added. Losing either
///     makes every list using that name fail to load, so each accepted name is checked here, from the
///     attributes and from a fixed table that a deleted alias cannot quietly leave.
/// </summary>
public class TypeNameTests
{
    private readonly DTOSerializer _serializer;

    public TypeNameTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <summary>
    ///     Every name declared by an attribute today, with the type and family it belongs to.
    /// </summary>
    public static IEnumerable<object[]> DeclaredNames =>
        ModlistTypes.AllConcreteTypes()
            .SelectMany(t => ModlistTypes.JsonAliases(t.Type).Append(ModlistTypes.JsonName(t.Type))
                .Where(n => n != null)
                .Select(n => new object[] {n, t.Family, t.Type}));

    /// <summary>
    ///     Every name the readers accepted when these tests were written. Removing a row means a list
    ///     that uses the name can no longer be opened.
    /// </summary>
    public static IEnumerable<object[]> AcceptedNames =>
        new List<object[]>
        {
            new object[] {"Bethesda", typeof(IDownloadState), typeof(Bethesda)},
            new object[] {"LoversLabDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(DeprecatedLoversLab)},
            new object[] {"GameFileSourceDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(GameFileSource)},
            new object[] {"GameFileSource", typeof(IDownloadState), typeof(GameFileSource)},
            new object[] {"GoogleDriveDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(GoogleDrive)},
            new object[] {"GoogleDrive", typeof(IDownloadState), typeof(GoogleDrive)},
            new object[] {"HttpDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(Http)},
            new object[] {"HttpDownloader", typeof(IDownloadState), typeof(Http)},
            new object[] {"Http", typeof(IDownloadState), typeof(Http)},
            new object[] {"LoversLabOAuthDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(LoversLab)},
            new object[] {"LoversLab", typeof(IDownloadState), typeof(LoversLab)},
            new object[] {"ManualDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(Manual)},
            new object[] {"Manual", typeof(IDownloadState), typeof(Manual)},
            new object[] {"MediaFireDownloader+State, Wabbajack.Lib", typeof(IDownloadState), typeof(MediaFire)},
            new object[] {"MediaFireDownloader", typeof(IDownloadState), typeof(MediaFire)},
            new object[] {"MediaFire", typeof(IDownloadState), typeof(MediaFire)},
            new object[] {"MegaDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(Mega)},
            new object[] {"Mega", typeof(IDownloadState), typeof(Mega)},
            new object[] {"ModDBDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(ModDB)},
            new object[] {"ModDBDownloader", typeof(IDownloadState), typeof(ModDB)},
            new object[] {"ModDB", typeof(IDownloadState), typeof(ModDB)},
            new object[] {"NexusDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(Nexus)},
            new object[] {"Nexus", typeof(IDownloadState), typeof(Nexus)},
            new object[] {"TESAllianceDownloader, Wabbajack.Lib", typeof(IDownloadState), typeof(TESAlliance)},
            new object[] {"TESAlliance", typeof(IDownloadState), typeof(TESAlliance)},
            new object[] {"VectorPlexusOAuthDownloader+State, Wabbajack.Lib", typeof(IDownloadState), typeof(VectorPlexus)},
            new object[] {"VectorPlexus", typeof(IDownloadState), typeof(VectorPlexus)},
            new object[] {"WabbajackCDNDownloader+State, Wabbajack.Lib", typeof(IDownloadState), typeof(WabbajackCDN)},
            new object[] {"WabbajackCDN", typeof(IDownloadState), typeof(WabbajackCDN)},

            new object[] {"ArchiveMeta", typeof(Directive), typeof(ArchiveMeta)},
            new object[] {"ArchiveMeta, Wabbajack.Lib", typeof(Directive), typeof(ArchiveMeta)},
            new object[] {"CreateBSA", typeof(Directive), typeof(CreateBSA)},
            new object[] {"CreateBSA, Wabbajack.Lib", typeof(Directive), typeof(CreateBSA)},
            new object[] {"FromArchive", typeof(Directive), typeof(FromArchive)},
            new object[] {"FromArchive, Wabbajack.Lib", typeof(Directive), typeof(FromArchive)},
            new object[] {"IgnoredDirectly", typeof(Directive), typeof(IgnoredDirectly)},
            new object[] {"InlineFile", typeof(Directive), typeof(InlineFile)},
            new object[] {"InlineFile, Wabbajack.Lib", typeof(Directive), typeof(InlineFile)},
            new object[] {"MergedPatch, Wabbajack.Lib", typeof(Directive), typeof(MergedPatch)},
            new object[] {"MergedPatch", typeof(Directive), typeof(MergedPatch)},
            new object[] {"NoMatch", typeof(Directive), typeof(NoMatch)},
            new object[] {"PatchedFromArchive", typeof(Directive), typeof(PatchedFromArchive)},
            new object[] {"PatchedFromArchive, Wabbajack.Lib", typeof(Directive), typeof(PatchedFromArchive)},
            new object[] {"PropertyFile", typeof(Directive), typeof(PropertyFile)},
            new object[] {"PropertyFile, Wabbajack.Lib", typeof(Directive), typeof(PropertyFile)},
            new object[] {"RemappedInlineFile", typeof(Directive), typeof(RemappedInlineFile)},
            new object[] {"RemappedInlineFile, Wabbajack.Lib", typeof(Directive), typeof(RemappedInlineFile)},
            new object[] {"TransformedTexture", typeof(Directive), typeof(TransformedTexture)},
            new object[] {"TransformedTexture, Wabbajack.Lib", typeof(Directive), typeof(TransformedTexture)},

            new object[] {"BA2State, Compression.BSA", typeof(IArchive), typeof(BA2State)},
            new object[] {"BA2State", typeof(IArchive), typeof(BA2State)},
            new object[] {"BSAState, Compression.BSA", typeof(IArchive), typeof(BSAState)},
            new object[] {"BSAState", typeof(IArchive), typeof(BSAState)},
            new object[] {"TES3State", typeof(IArchive), typeof(TES3State)},
            new object[] {"TES3State, Compression.BSA", typeof(IArchive), typeof(TES3State)},

            new object[] {"BA2DX10Entry", typeof(AFile), typeof(BA2DX10File)},
            new object[] {"BA2DX10Entry, Compression.BSA", typeof(AFile), typeof(BA2DX10File)},
            new object[] {"BA2File", typeof(AFile), typeof(BA2File)},
            new object[] {"BA2FileEntryState, Compression.BSA", typeof(AFile), typeof(BA2File)},
            new object[] {"BSAFileState, Compression.BSA", typeof(AFile), typeof(BSAFile)},
            new object[] {"BSAFile", typeof(AFile), typeof(BSAFile)},
            new object[] {"TES3File", typeof(AFile), typeof(TES3File)}
        };

    /// <summary>
    ///     Reading a value written under any accepted name gives the right type with every property
    ///     intact. Driven by the attributes, so an alias added without regenerating Generated.cs fails.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeclaredNames))]
    public void EveryDeclaredNameReadsAsItsType(string name, Type family, Type type)
    {
        AssertReadsAs(name, family, type);
    }

    [Theory]
    [MemberData(nameof(AcceptedNames))]
    public void EveryPreviouslyAcceptedNameStillReadsAsItsType(string name, Type family, Type type)
    {
        AssertReadsAs(name, family, type);
    }

    /// <summary>
    ///     The table above and the attributes have to agree. A name only in the attributes is new and
    ///     belongs in the table; a name only in the table has been deleted, which breaks old lists.
    /// </summary>
    [Fact]
    public void AcceptedNamesTableMatchesTheAttributes()
    {
        static string Key(object[] row) => $"{((Type) row[1]).Name} | {row[0]} -> {((Type) row[2]).Name}";

        var declared = DeclaredNames.Select(Key).ToHashSet();
        var table = AcceptedNames.Select(Key).ToHashSet();

        Assert.Empty(declared.Except(table));
        Assert.Empty(table.Except(declared));
    }

    private void AssertReadsAs(string name, Type family, Type type)
    {
        var original = new SampleBuilder().Build(type);
        var json = JsonSerializer.Serialize(original, family, _serializer.Options);

        // Swap the name by text rather than through JsonNode, so the rest of the document is read exactly as
        // the converter wrote it.
        var prefix = "{\"$type\":" + JsonSerializer.Serialize(ModlistTypes.JsonName(type));
        Assert.StartsWith(prefix, json);
        json = "{\"$type\":" + JsonSerializer.Serialize(name) + json[prefix.Length..];

        var read = JsonSerializer.Deserialize(json, family, _serializer.Options);

        Assert.IsType(type, read);
        DeepCompare.AssertEqual(original, read, $"{name}");
    }

    public static IEnumerable<object[]> FamilyRows => ModlistTypes.Families.Select(f => new object[] {f});

    /// <summary>
    ///     An unknown <c>$type</c> throws a <see cref="JsonException" /> naming it rather than being
    ///     skipped or read as null. This is what an older Wabbajack does when it opens a list compiled by
    ///     a newer one that uses a source or directive it has never heard of: the whole list fails to
    ///     load with that message, rather than installing with a hole in it. If that is ever changed to
    ///     something more lenient, it has to be on purpose, and the installer has to cope with the gap.
    /// </summary>
    [Theory]
    [MemberData(nameof(FamilyRows))]
    public void AnUnknownTypeNameThrowsNamingIt(Type family)
    {
        const string json = """{"$type": "FutureThing, Wabbajack.Lib", "Url": "https://example.com/a.zip"}""";

        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, family, _serializer.Options));
        Assert.Contains("No Type dispatch for FutureThing, Wabbajack.Lib", ex.Message);
    }

    /// <summary>
    ///     One unknown download state anywhere in the archive list fails the whole modlist.
    /// </summary>
    [Fact]
    public void AnUnknownDownloadStateFailsTheWholeModList()
    {
        const string json = """
            {
              "Archives": [
                {"Hash": "LqKnmSUrXac=", "Name": "a.7z", "Size": 1, "State": {"$type": "HttpDownloader, Wabbajack.Lib", "Headers": [], "Url": "https://example.com/a.7z"}},
                {"Hash": "/brB4w0cf7I=", "Name": "b.7z", "Size": 2, "State": {"$type": "TorrentDownloader, Wabbajack.Lib", "Magnet": "magnet:?xt=urn:btih:0"}}
              ],
              "Directives": [],
              "GameType": "SkyrimSpecialEdition",
              "Name": "Future list"
            }
            """;

        var ex = Assert.Throws<JsonException>(() => _serializer.Deserialize<ModList>(json));
        Assert.Contains("TorrentDownloader, Wabbajack.Lib", ex.Message);
    }

    /// <summary>
    ///     The reader looks for <c>$type</c> as the very first property and throws otherwise. Wabbajack
    ///     has always written it first, but a list re-serialized by another tool that sorts keys would
    ///     not load. Pinned so that a change either way is deliberate.
    /// </summary>
    [Theory]
    [MemberData(nameof(FamilyRows))]
    public void TypeMustBeTheFirstProperty(Type family)
    {
        var name = ModlistTypes.JsonName(ModlistTypes.ConcreteTypes(family).First());
        var json = $$"""{"Size": 1, "$type": "{{name}}"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, family, _serializer.Options));
    }

    [Theory]
    [MemberData(nameof(FamilyRows))]
    public void AMissingTypeThrows(Type family)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"Size": 1}""", family, _serializer.Options));
    }

    /// <summary>
    ///     Properties a reader does not know are skipped. That is what lets a newer Wabbajack add a field
    ///     to an existing state or directive without breaking older readers.
    /// </summary>
    [Fact]
    public void UnknownPropertiesAreIgnored()
    {
        const string json = """
            {
              "$type": "NexusDownloader, Wabbajack.Lib",
              "AddedLater": {"Nested": [1, 2, {"Deep": true}]},
              "GameName": "SkyrimSpecialEdition",
              "ModID": 266,
              "AnotherNewOne": "text",
              "FileID": 1000172397
            }
            """;

        var state = Assert.IsType<Nexus>(_serializer.Deserialize<IDownloadState>(json));
        Assert.Equal(Game.SkyrimSpecialEdition, state.Game);
        Assert.Equal(266, state.ModID);
        Assert.Equal(1000172397, state.FileID);
    }

    /// <summary>
    ///     Properties a writer left out come back as their defaults, which is how lists from before a
    ///     property existed are read.
    /// </summary>
    [Fact]
    public void MissingPropertiesReadAsDefaults()
    {
        var state = Assert.IsType<Http>(_serializer.Deserialize<IDownloadState>(
            """{"$type": "HttpDownloader, Wabbajack.Lib", "Url": "https://example.com/a.7z"}"""));
        Assert.Equal(new Uri("https://example.com/a.7z"), state.Url);

        var directive = Assert.IsType<PatchedFromArchive>(_serializer.Deserialize<Directive>(
            """{"$type": "PatchedFromArchive, Wabbajack.Lib", "To": "a.esp"}"""));
        Assert.Equal("a.esp", directive.To.ToString());
        Assert.Equal(default, directive.FromHash);
        Assert.Equal(0, directive.Size);
    }

    /// <summary>
    ///     DTOSerializer allows numbers written as strings, and the generated converters pass its options
    ///     down, so a size quoted by some other tool still reads.
    /// </summary>
    [Fact]
    public void NumbersMayBeQuoted()
    {
        var directive = Assert.IsType<FromArchive>(_serializer.Deserialize<Directive>(
            """{"$type": "FromArchive", "Size": "191488", "To": "a.dll"}"""));
        Assert.Equal(191488, directive.Size);
    }
}
