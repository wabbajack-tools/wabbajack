using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;

namespace Wabbajack.Compiler.CompilationSteps;

public class DropAll : ACompilationStep
{
    public DropAll(ACompiler compiler) : base(compiler)
    {
    }

    public override ValueTask<Directive?> Run(RawSourceFile source)
    {
        var result = source.EvolveTo<NoMatch>();
        result.Reason = "No Match in Stack";
        return ValueTask.FromResult<Directive?>(result);
    }
}