using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Directives;

[JsonName("PatchedFromArchive")]
[JsonAlias("PatchedFromArchive, Wabbajack.Lib")]
public class PatchedFromArchive : FromArchive
{
    public Hash FromHash { get; set; }

    /// <summary>
    ///     The file to apply to the source file to patch it
    /// </summary>
    public RelativePath PatchID { get; set; }
}