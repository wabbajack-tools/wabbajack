using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.CDN;

public class PartDefinition
{
    public long Size { get; set; }
    public long Offset { get; set; }
    public Hash Hash { get; set; }
    public long Index { get; set; }
}