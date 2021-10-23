using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BSA.ArchiveStates;

[JsonName("TES3State")]
[JsonAlias("TES3State, Compression.BSA")]
public class TES3State : IArchive
{
    public uint FileCount { get; set; }
    public long DataOffset { get; set; }
    public uint HashOffset { get; set; }
    public uint VersionNumber { get; set; }
}