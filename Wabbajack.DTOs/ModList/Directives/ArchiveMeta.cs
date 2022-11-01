using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Directives;

[JsonName("ArchiveMeta")]
[JsonAlias("ArchiveMeta, Wabbajack.Lib")]
public class ArchiveMeta : Directive
{
    public RelativePath SourceDataID { get; set; }

    public override bool IsDeterministic => false;
}