using System;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Directives;

[JsonName("MergedPatch, Wabbajack.Lib")]
[JsonAlias("MergedPatch")]
public class MergedPatch : Directive
{
    public RelativePath PatchID { get; set; }
    public SourcePatch[] Sources { get; set; } = Array.Empty<SourcePatch>();
}