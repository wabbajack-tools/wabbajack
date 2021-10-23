using Wabbajack.DTOs.JsonConverters;

namespace Wabbajack.DTOs.BSA.FileStates;

[JsonName("BSAFileState, Compression.BSA")]
[JsonAlias("BSAFile")]
public class BSAFile : AFile
{
    public bool FlipCompression { get; set; }
}