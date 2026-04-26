using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeAll : ACompilationStep
{
    public IncludeAll(ACompiler compiler) : base(compiler)
    {
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        var inline = source.EvolveTo<InlineFile>();
        _compiler._sourceFileLinks[inline] = source;
        return inline;
    }
}