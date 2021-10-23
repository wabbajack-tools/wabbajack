using Wabbajack.Paths;

namespace Wabbajack.Compiler;

public class MO2CompilerSettings : CompilerSettings
{
    public string Profile { get; set; }
    public RelativePath[] AlwaysEnabled { get; set; }
}