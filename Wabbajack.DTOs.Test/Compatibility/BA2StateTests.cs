using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.JsonConverters;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Starfield's v2 and v3 archives carry two more header fields and a compression type, which the installer
///     writes back when it rebuilds a BA2 and which choose LZ4 over zlib for v3 textures. They were added to
///     BA2State without the converters being regenerated, so a compiled list lost them and its archives were
///     rebuilt with zeros. A list compiled before they existed has to keep loading as it did.
/// </summary>
public class BA2StateTests
{
    private readonly DTOSerializer _serializer;

    public BA2StateTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    [Fact]
    public void StarfieldHeaderFields_AreKept()
    {
        var state = new BA2State
        {
            HasNameTable = true, HeaderMagic = "BTDX", Type = BA2EntryType.DX10, Version = 3,
            Unknown1 = 1, Unknown2 = 2, Compression = 3
        };

        var read = Assert.IsType<BA2State>(_serializer.Deserialize<IArchive>(_serializer.Serialize<IArchive>(state)));

        Assert.Equal(1u, read.Unknown1);
        Assert.Equal(2u, read.Unknown2);
        Assert.Equal(3u, read.Compression);
    }

    [Fact]
    public void ListFromBeforeStarfield_StillLoads()
    {
        var read = Assert.IsType<BA2State>(_serializer.Deserialize<IArchive>(
            """{"$type":"BA2State, Compression.BSA","HasNameTable":true,"HeaderMagic":"BTDX","Type":0,"Version":1}"""));

        Assert.Equal(1u, read.Version);
        Assert.Equal(0u, read.Unknown1);
        Assert.Equal(0u, read.Compression);
    }
}
