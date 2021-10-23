using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Directives;

public class SourcePatch
{
    public Hash Hash { get; set; }
    public RelativePath RelativePath { get; set; }
}