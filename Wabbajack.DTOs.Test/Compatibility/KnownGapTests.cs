using System.Text.Json;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Modlist format problems these tests found in the current code. Each is skipped so the offline
///     lane stays green, and each states the behaviour it expects, so removing the Skip is the check
///     that the fix works.
/// </summary>
public class KnownGapTests
{
    private readonly DTOSerializer _serializer;

    public KnownGapTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    /// <summary>
    ///     Unknown1, Unknown2 and Compression were added to BA2State for Starfield's v2/v3 archives
    ///     without Generated.cs being regenerated, so its converter neither writes nor reads them. A
    ///     compiled list loses them and the installer rebuilds the BA2 with zeros in their place, which
    ///     for a v3 archive also switches its textures from LZ4 to zlib. Remove the three entries from
    ///     <see cref="ModlistTypes.KnownDroppedProperties" /> when this passes.
    /// </summary>
    [Fact(Skip = "Generated.cs is stale for BA2State; rerun Wabbajack.DTOs.ConverterGenerators")]
    public void BA2StateKeepsItsStarfieldHeaderFields()
    {
        var state = new BA2State
        {
            HasNameTable = true, HeaderMagic = "BTDX", Type = BA2EntryType.DX10, Version = 3,
            Unknown1 = 1, Unknown2 = 2, Compression = 3
        };

        var read = Assert.IsType<BA2State>(
            _serializer.Deserialize<IArchive>(_serializer.Serialize<IArchive>(state)));

        Assert.Equal(1u, read.Unknown1);
        Assert.Equal(2u, read.Unknown2);
        Assert.Equal(3u, read.Compression);
    }

    /// <summary>
    ///     HashJsonConverter decodes <c>reader.ValueSpan</c>, which is the raw, still-escaped text. JSON
    ///     lets a writer escape any character, and System.Text.Json's default writer escapes "+", so a
    ///     modlist passed through JsonNode, or any tool that escapes "/" as "\/", has its hashes silently
    ///     read as different values. The decode error is swallowed, so nothing says so.
    /// </summary>
    [Fact(Skip = "HashJsonConverter.Read ignores JSON escapes in the hash string")]
    public void EscapedHashTextReadsAsTheSameHash()
    {
        var plus = _serializer.Deserialize<Directive>(
            """{"$type": "FromArchive", "Hash": "\u002B/+/+/+/+/8="}""")!;
        var slash = _serializer.Deserialize<Directive>(
            """{"$type": "FromArchive", "Hash": "\/brB4w0cf7I="}""")!;

        Assert.Equal(Hash.FromULong(0xFFFBBFFFFBBFFFFB), plus.Hash);
        Assert.Equal(Hash.FromULong(0xB27F1C0DE3C1BAFD), slash.Hash);
    }

    /// <summary>
    ///     The generated readers assign every property, present or not, so a property left out of the
    ///     JSON is set to null over its initializer. For Http that makes Headers null, and
    ///     HttpDownloader enumerates Headers when it builds the request.
    /// </summary>
    [Fact(Skip = "Generated readers overwrite property initializers with null when the property is absent")]
    public void AbsentArrayPropertiesKeepTheirInitializer()
    {
        var http = Assert.IsType<Http>(_serializer.Deserialize<IDownloadState>(
            """{"$type": "HttpDownloader, Wabbajack.Lib", "Url": "https://example.com/a.7z"}"""));

        Assert.NotNull(http.Headers);
        Assert.Empty(http.Headers);
    }
}
