using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BSA.ArchiveStates;

public enum BA2EntryType
{
    GNRL,
    DX10,
    GNMF
}

[JsonName("BA2State, Compression.BSA")]
[JsonAlias("BA2State")]
public class BA2State : IArchive
{
    public bool HasNameTable { get; set; }
    public BA2EntryType Type { get; set; }
    public string HeaderMagic { get; set; }
    public uint Version { get; set; }
    public uint Unknown1 { get; set; }
    public uint Unknown2 { get; set; }
    public uint Compression { get; set; }
}