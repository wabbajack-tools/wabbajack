using System;
using Wabbajack.Paths;

namespace Wabbajack.Compiler;

public class MO2CompilerSettings : CompilerSettings
{
    public string Profile { get; set; } = "";
    public RelativePath[] AlwaysEnabled { get; set; } = Array.Empty<RelativePath>();
    public string[] OtherProfiles { get; set; }
}