using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BSA.FileStates;

[JsonName("BA2File")]
[JsonAlias("BA2FileEntryState, Compression.BSA")]
public class BA2File : AFile
{
    public string Extension { get; set; }
    public bool Compressed { get; set; }
    public uint Align { get; set; }
    public uint Flags { get; set; }
    public uint DirHash { get; set; }
    public uint NameHash { get; set; }
}