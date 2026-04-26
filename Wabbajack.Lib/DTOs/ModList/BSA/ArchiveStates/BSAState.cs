using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BSA.ArchiveStates;

[JsonName("BSAState, Compression.BSA")]
[JsonAlias("BSAState")]
public class BSAState : IArchive
{
    public string Magic { get; set; } = string.Empty;
    public uint Version { get; set; }
    public uint ArchiveFlags { get; set; }
    public uint FileFlags { get; set; }
}