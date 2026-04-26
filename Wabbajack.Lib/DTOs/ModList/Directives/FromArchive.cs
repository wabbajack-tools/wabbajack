using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.Directives;

[JsonName("FromArchive")]
[JsonAlias("FromArchive, Wabbajack.Lib")]
public class FromArchive : Directive
{
    public HashRelativePath ArchiveHashPath { get; set; }
}