using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.Configs;

public class ForcedRemoval
{
    public string Name { get; set; }
    public string Reason { get; set; }
    public Hash Hash { get; set; }
}