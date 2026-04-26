using System;
using Wabbajack.DTOs.BSA.ArchiveStates;
using Wabbajack.DTOs.BSA.FileStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;

namespace Wabbajack.DTOs.Directives;

[JsonName("CreateBSA")]
[JsonAlias("CreateBSA, Wabbajack.Lib")]
public class CreateBSA : Directive
{
    public RelativePath TempID { get; set; }
    public IArchive State { get; set; }
    public AFile[] FileStates { get; set; } = Array.Empty<AFile>();

    public override bool IsDeterministic => false;
}